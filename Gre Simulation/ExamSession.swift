import Foundation
import Combine

enum ExamPhase: Equatable {
    case directions
    case testing
    case completed
}

@MainActor
final class ExamSession: ObservableObject {
    let mode: ExamMode

    @Published private(set) var sections: [ExamSection]
    @Published private(set) var currentSectionIndex = 0
    @Published var currentQuestionIndex = 0
    @Published var answers: [String: GREAnswer] = [:]
    @Published var markedQuestionIDs: Set<String> = []
    @Published private(set) var phase: ExamPhase = .directions
    @Published private(set) var secondsRemaining = 0
    @Published var isTimeVisible = true
    @Published var isCalculatorPresented = false
    @Published private(set) var result: ExamResult?

    private let startedAt = Date()
    private var sectionDeadline: Date?
    private var completedSectionIDs: Set<String> = []
    private var sectionSummaries: [SectionSummary] = []

    init(mode: ExamMode) {
        self.mode = mode
        self.sections = Self.makeSections(mode: mode)
    }

    var currentSection: ExamSection { sections[currentSectionIndex] }
    var currentQuestion: GREQuestion { currentSection.questions[currentQuestionIndex] }
    var currentAnswer: GREAnswer { answers[currentQuestion.id] ?? GREAnswer() }
    var isCurrentMarked: Bool { markedQuestionIDs.contains(currentQuestion.id) }
    var answeredCount: Int { currentSection.questions.filter { !(answers[$0.id] ?? GREAnswer()).isEmpty }.count }
    var markedCount: Int { currentSection.questions.filter { markedQuestionIDs.contains($0.id) }.count }
    var canGoBack: Bool { currentQuestionIndex > 0 }
    var isLastQuestion: Bool { currentQuestionIndex == currentSection.questions.count - 1 }
    var progress: Double { Double(currentQuestionIndex + 1) / Double(max(1, currentSection.questions.count)) }

    func beginSection() {
        secondsRemaining = currentSection.durationSeconds
        sectionDeadline = Date().addingTimeInterval(TimeInterval(secondsRemaining))
        phase = .testing
        currentQuestionIndex = 0
    }

    func tick() {
        guard phase == .testing, let sectionDeadline else { return }
        secondsRemaining = max(0, Int(ceil(sectionDeadline.timeIntervalSinceNow)))
        if secondsRemaining == 0 {
            finishSection()
        }
    }

    func goBack() {
        guard canGoBack else { return }
        currentQuestionIndex -= 1
    }

    func goNext() {
        guard !isLastQuestion else { return }
        currentQuestionIndex += 1
    }

    func goToQuestion(_ index: Int) {
        guard currentSection.questions.indices.contains(index) else { return }
        currentQuestionIndex = index
    }

    func toggleMark() {
        if isCurrentMarked {
            markedQuestionIDs.remove(currentQuestion.id)
        } else {
            markedQuestionIDs.insert(currentQuestion.id)
        }
    }

    func toggleSelection(groupID: String, optionID: String, maximum: Int) {
        var answer = currentAnswer
        var selections = answer.selections[groupID] ?? []
        if selections.contains(optionID) {
            selections.remove(optionID)
        } else if maximum == 1 {
            selections = [optionID]
        } else if selections.count < maximum {
            selections.insert(optionID)
        }
        answer.selections[groupID] = selections
        answers[currentQuestion.id] = answer
    }

    func updateNumericEntry(_ text: String) {
        var answer = currentAnswer
        answer.numericEntry = text
        answers[currentQuestion.id] = answer
    }

    func updateEssay(_ text: String) {
        var answer = currentAnswer
        answer.essayText = text
        answers[currentQuestion.id] = answer
    }

    func finishSection() {
        guard phase == .testing, !completedSectionIDs.contains(currentSection.id) else { return }
        let section = currentSection
        completedSectionIDs.insert(section.id)
        sectionDeadline = nil

        let scored = section.questions.filter(\.isScored)
        let correct = scored.filter { isCorrect($0) }.count
        let answered = section.questions.filter { !(answers[$0.id] ?? GREAnswer()).isEmpty }.count
        sectionSummaries.append(SectionSummary(
            measure: section.measure,
            ordinal: section.ordinal,
            difficulty: section.adaptiveDifficulty,
            correct: correct,
            total: scored.count,
            answered: answered
        ))

        if section.ordinal == 1, section.measure == .verbal || section.measure == .quantitative {
            configureAdaptiveSecondSection(for: section.measure, firstSectionCorrect: correct, total: scored.count)
        }

        if currentSectionIndex + 1 < sections.count {
            currentSectionIndex += 1
            currentQuestionIndex = 0
            phase = .directions
        } else {
            completeExam()
        }
    }

    func isCorrect(_ question: GREQuestion) -> Bool {
        let answer = answers[question.id] ?? GREAnswer()
        if question.format == .numericEntry {
            return question.acceptedNumericAnswers.contains { NumericAnswerMatcher.matches(answer.numericEntry, $0) }
        }
        guard question.format != .essay else { return false }
        return question.correctSelections.allSatisfy { groupID, expected in
            (answer.selections[groupID] ?? []) == expected
        }
    }

    func status(for question: GREQuestion) -> QuestionStatus {
        if markedQuestionIDs.contains(question.id) { return .marked }
        if !(answers[question.id] ?? GREAnswer()).isEmpty { return .answered }
        return .unanswered
    }

    private func configureAdaptiveSecondSection(for measure: GREMeasure, firstSectionCorrect: Int, total: Int) {
        guard let targetIndex = sections.firstIndex(where: { $0.measure == measure && $0.ordinal == 2 }) else { return }
        let accuracy = total == 0 ? 0 : Double(firstSectionCorrect) / Double(total)
        let route: QuestionDifficulty = accuracy >= 2.0 / 3.0 ? .hard : (accuracy >= 0.4 ? .medium : .easy)
        let used = Set(sections.first(where: { $0.measure == measure && $0.ordinal == 1 })?.questions.map(\.id) ?? [])
        let questions = QuestionBank.questions(for: measure, difficulty: route, count: 15, excluding: used)
        sections[targetIndex] = ExamSection(
            id: "\(measure.rawValue)-2-\(route.rawValue)",
            measure: measure,
            ordinal: 2,
            durationSeconds: measure == .verbal ? 23 * 60 : 26 * 60,
            questions: questions,
            adaptiveDifficulty: route
        )
    }

    private func completeExam() {
        phase = .completed
        let verbal = totals(for: .verbal)
        let quant = totals(for: .quantitative)
        let writingQuestionID = sections
            .first(where: { $0.measure == .analyticalWriting })?
            .questions.first?.id
        let essay = writingQuestionID.flatMap { answers[$0]?.essayText } ?? ""
        let verbalAnswered = sectionSummaries.filter { $0.measure == .verbal }.reduce(0) { $0 + $1.answered }
        let quantAnswered = sectionSummaries.filter { $0.measure == .quantitative }.reduce(0) { $0 + $1.answered }

        result = ExamResult(
            id: UUID(),
            completedAt: Date(),
            mode: mode,
            verbalScore: verbal.total > 0 && verbalAnswered > 0 ? ScoreEstimator.scaled(correct: verbal.correct, total: verbal.total) : nil,
            quantitativeScore: quant.total > 0 && quantAnswered > 0 ? ScoreEstimator.scaled(correct: quant.correct, total: quant.total) : nil,
            writingEstimate: essay.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : WritingEstimator.estimate(essay),
            verbalCorrect: verbal.correct,
            verbalTotal: verbal.total,
            quantitativeCorrect: quant.correct,
            quantitativeTotal: quant.total,
            elapsedSeconds: max(0, Int(Date().timeIntervalSince(startedAt))),
            sections: sectionSummaries
        )
    }

    private func totals(for measure: GREMeasure) -> (correct: Int, total: Int) {
        sectionSummaries
            .filter { $0.measure == measure }
            .reduce((0, 0)) { ($0.0 + $1.correct, $0.1 + $1.total) }
    }

    private static func makeSections(mode: ExamMode) -> [ExamSection] {
        let verbalOne = QuestionBank.questions(for: .verbal, count: 12)
        let quantOne = QuestionBank.questions(for: .quantitative, count: 12)

        switch mode {
        case .fullLength:
            let verbalUsed = Set(verbalOne.map(\.id))
            let quantUsed = Set(quantOne.map(\.id))
            let writingQuestion = QuestionBank.writingQuestions.randomElement() ?? QuestionBank.writingQuestion
            return [
                ExamSection(id: "writing-1", measure: .analyticalWriting, ordinal: 1, durationSeconds: 30 * 60, questions: [writingQuestion], adaptiveDifficulty: nil),
                ExamSection(id: "verbal-1", measure: .verbal, ordinal: 1, durationSeconds: 18 * 60, questions: verbalOne, adaptiveDifficulty: nil),
                ExamSection(id: "quantitative-1", measure: .quantitative, ordinal: 1, durationSeconds: 21 * 60, questions: quantOne, adaptiveDifficulty: nil),
                ExamSection(id: "verbal-2-medium", measure: .verbal, ordinal: 2, durationSeconds: 23 * 60, questions: QuestionBank.questions(for: .verbal, difficulty: .medium, count: 15, excluding: verbalUsed), adaptiveDifficulty: .medium),
                ExamSection(id: "quantitative-2-medium", measure: .quantitative, ordinal: 2, durationSeconds: 26 * 60, questions: QuestionBank.questions(for: .quantitative, difficulty: .medium, count: 15, excluding: quantUsed), adaptiveDifficulty: .medium)
            ]
        case .verbalPractice:
            return [ExamSection(id: "verbal-practice", measure: .verbal, ordinal: 1, durationSeconds: 18 * 60, questions: verbalOne, adaptiveDifficulty: nil)]
        case .quantitativePractice:
            return [ExamSection(id: "quant-practice", measure: .quantitative, ordinal: 1, durationSeconds: 21 * 60, questions: quantOne, adaptiveDifficulty: nil)]
        }
    }

    #if DEBUG
    func completeWithCorrectAnswersForTesting() {
        var safetyCounter = 0
        while phase != .completed, safetyCounter < 10 {
            safetyCounter += 1
            if phase == .directions { beginSection() }
            for question in currentSection.questions {
                if question.format == .essay {
                    answers[question.id] = GREAnswer(
                        essayText: Array(repeating: "A clear position should examine evidence, acknowledge reasonable limits, and explain why the conclusion follows. ", count: 35).joined()
                    )
                } else if question.format == .numericEntry {
                    answers[question.id] = GREAnswer(numericEntry: question.acceptedNumericAnswers.sorted().first ?? "")
                } else {
                    answers[question.id] = GREAnswer(selections: question.correctSelections)
                }
            }
            finishSection()
        }
        assert(phase == .completed)
        assert(result?.verbalScore == nil || result?.verbalScore == 170)
        assert(result?.quantitativeScore == nil || result?.quantitativeScore == 170)
    }
    #endif
}

enum QuestionStatus {
    case unanswered
    case answered
    case marked
}

enum NumericAnswerMatcher {
    static func matches(_ entered: String, _ expected: String) -> Bool {
        let enteredText = normalize(entered)
        let expectedText = normalize(expected)
        if enteredText == expectedText { return true }
        guard let enteredValue = value(enteredText), let expectedValue = value(expectedText) else { return false }
        return abs(enteredValue - expectedValue) < 0.000_000_1
    }

    private static func normalize(_ value: String) -> String {
        value.trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: ",", with: "")
            .replacingOccurrences(of: " ", with: "")
            .lowercased()
    }

    private static func value(_ text: String) -> Double? {
        let pieces = text.split(separator: "/", omittingEmptySubsequences: false)
        if pieces.count == 1 { return Double(text) }
        guard pieces.count == 2, let numerator = Double(pieces[0]),
              let denominator = Double(pieces[1]), denominator != 0 else { return nil }
        return numerator / denominator
    }
}

enum ScoreEstimator {
    static func scaled(correct: Int, total: Int) -> Int {
        guard total > 0 else { return 130 }
        let raw = 130.0 + 40.0 * Double(correct) / Double(total)
        return min(170, max(130, Int(raw.rounded())))
    }
}

enum WritingEstimator {
    static func estimate(_ essay: String) -> Double {
        let words = essay.split { $0.isWhitespace || $0.isPunctuation }
        let wordCount = words.count
        guard wordCount >= 25 else { return 0.5 }

        let lowerWords = words.map { $0.lowercased() }
        let uniqueRatio = Double(Set(lowerWords).count) / Double(max(1, wordCount))
        let paragraphCount = essay.components(separatedBy: "\n").filter {
            $0.trimmingCharacters(in: .whitespacesAndNewlines).count > 30
        }.count
        let reasoningMarkers = ["because", "however", "therefore", "although", "for example", "consequently", "while", "nevertheless"]
            .filter { essay.lowercased().contains($0) }.count

        var score: Double
        switch wordCount {
        case ..<100: score = 1.5
        case ..<180: score = 2.5
        case ..<280: score = 3.5
        case ..<420: score = 4.0
        default: score = 4.5
        }
        if paragraphCount >= 4 { score += 0.5 }
        if reasoningMarkers >= 4 { score += 0.5 }
        if wordCount > 180 && uniqueRatio < 0.38 { score -= 0.5 }
        return min(6.0, max(0.5, (score * 2).rounded() / 2))
    }
}
