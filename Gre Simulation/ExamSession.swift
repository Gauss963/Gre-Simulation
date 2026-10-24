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
    @Published var currentQuestionIndex = 0 {
        didSet { encounteredIDs.insert(currentQuestion.id) }
    }
    @Published var answers: [String: GREAnswer] = [:]
    @Published var markedQuestionIDs: Set<String> = []
    @Published private(set) var encounteredIDs: Set<String> = []
    @Published private(set) var phase: ExamPhase = .directions
    @Published private(set) var secondsRemaining = 0
    @Published var isTimeVisible = true
    @Published var isCalculatorPresented = false
    @Published private(set) var result: ExamResult?

    /// Kept for the whole test, like the on-screen calculator in the Quantitative sections.
    let calculator = CalculatorModel()

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
    var answeredCount: Int { currentSection.questions.filter { status(for: $0) == .answered }.count }
    var markedCount: Int { currentSection.questions.filter { markedQuestionIDs.contains($0.id) }.count }
    var canGoBack: Bool { currentQuestionIndex > 0 }
    var isLastQuestion: Bool { currentQuestionIndex == currentSection.questions.count - 1 }
    var progress: Double { Double(currentQuestionIndex + 1) / Double(max(1, currentSection.questions.count)) }
    var sectionPosition: String { "Section \(currentSectionIndex + 1) of \(sections.count)" }
    /// The clock reappears during the last five minutes of a section, as on the GRE.
    var isClockForcedVisible: Bool { secondsRemaining <= 300 }

    func beginSection() {
        secondsRemaining = currentSection.durationSeconds
        sectionDeadline = Date().addingTimeInterval(TimeInterval(secondsRemaining))
        phase = .testing
        currentQuestionIndex = 0
        encounteredIDs.insert(currentQuestion.id)
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

    /// Questions that have not been reached yet cannot be opened from the Review screen.
    func goToQuestion(_ index: Int) {
        guard currentSection.questions.indices.contains(index),
              encounteredIDs.contains(currentSection.questions[index].id) else { return }
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
        guard encounteredIDs.contains(question.id) else { return .notEncountered }
        let answer = answers[question.id] ?? GREAnswer()
        if answer.isEmpty { return .notAnswered }
        switch question.format {
        case .numericEntry:
            let parts = answer.numericEntry.split(separator: "/", omittingEmptySubsequences: false)
            return parts.contains { $0.trimmingCharacters(in: .whitespaces).isEmpty } ? .incomplete : .answered
        case .essay:
            return .answered
        case .sentenceEquivalence:
            return (answer.selections["main"]?.count ?? 0) == 2 ? .answered : .incomplete
        default:
            let allGroups = question.groups.allSatisfy { !(answer.selections[$0.id] ?? []).isEmpty }
            return allGroups ? .answered : .incomplete
        }
    }

    private func configureAdaptiveSecondSection(for measure: GREMeasure, firstSectionCorrect: Int, total: Int) {
        guard let targetIndex = sections.firstIndex(where: { $0.measure == measure && $0.ordinal == 2 }) else { return }
        let accuracy = total == 0 ? 0 : Double(firstSectionCorrect) / Double(total)
        let route: QuestionDifficulty = accuracy >= 2.0 / 3.0 ? .hard : (accuracy >= 0.4 ? .medium : .easy)
        let level: FormBuilder.Level = route == .hard ? .hard : (route == .easy ? .easy : .medium)
        let used = Set(sections.filter { $0.measure == measure && $0.ordinal == 1 }.flatMap { $0.questions.map(\.id) })
        let questions = measure == .verbal
            ? FormBuilder.verbalSection(count: 15, level: level, excluding: used)
            : FormBuilder.quantSection(count: 15, level: level, excluding: used)
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
        let writingQuestion = sections.first(where: { $0.measure == .analyticalWriting })?.questions.first
        let essay = writingQuestion.flatMap { answers[$0.id]?.essayText } ?? ""
        let hasEssay = !essay.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        let essayReport = writingQuestion.map { IssueEssayScorer.score(essay: essay, prompt: $0.prompt) }
        let questionReviews: [QuestionReviewRecord] = sections
            .filter { completedSectionIDs.contains($0.id) }
            .flatMap { section -> [QuestionReviewRecord] in
                section.questions.enumerated().compactMap { index, question -> QuestionReviewRecord? in
                    guard question.isScored else { return nil }
                    return QuestionReviewRecord(
                        sectionID: section.id,
                        sectionOrdinal: section.ordinal,
                        sectionDifficulty: section.adaptiveDifficulty,
                        questionNumber: index + 1,
                        question: question,
                        answer: answers[question.id] ?? GREAnswer(),
                        isCorrect: isCorrect(question)
                    )
                }
            }
        let verbal = totals(for: .verbal)
        let quant = totals(for: .quantitative)

        var result = ExamResult(
            id: UUID(),
            completedAt: Date(),
            mode: mode,
            verbalScore: scaledScore(for: .verbal),
            quantitativeScore: scaledScore(for: .quantitative),
            writingEstimate: hasEssay ? essayReport?.score : nil,
            verbalCorrect: verbal.correct,
            verbalTotal: verbal.total,
            quantitativeCorrect: quant.correct,
            quantitativeTotal: quant.total,
            elapsedSeconds: max(0, Int(Date().timeIntervalSince(startedAt))),
            sections: sectionSummaries,
            questionReviews: questionReviews
        )
        result.writingSummary = hasEssay ? essayReport?.summary : nil
        self.result = result
    }

    /// No score (nil) when no question of the measure was answered, as ETS reports "NS".
    private func scaledScore(for measure: GREMeasure) -> Int? {
        let summaries = sectionSummaries.filter { $0.measure == measure }
        guard !summaries.isEmpty, summaries.contains(where: { $0.answered > 0 }) else { return nil }
        let first = summaries.first { $0.ordinal == 1 }
        let second = summaries.first { $0.ordinal == 2 }
        return ScoreEstimator.scaled(
            measure: measure,
            firstCorrect: first?.correct ?? 0, firstTotal: first?.total ?? 0,
            secondCorrect: second?.correct ?? 0, secondTotal: second?.total ?? 0,
            route: second?.difficulty
        )
    }

    private func totals(for measure: GREMeasure) -> (correct: Int, total: Int) {
        sectionSummaries
            .filter { $0.measure == measure }
            .reduce((0, 0)) { ($0.0 + $1.correct, $0.1 + $1.total) }
    }

    private static func firstSection(_ measure: GREMeasure) -> ExamSection {
        let questions = measure == .verbal
            ? FormBuilder.verbalSection(count: 12, level: .average, excluding: [])
            : FormBuilder.quantSection(count: 12, level: .average, excluding: [])
        return ExamSection(
            id: "\(measure.rawValue)-1", measure: measure, ordinal: 1,
            durationSeconds: measure == .verbal ? 18 * 60 : 21 * 60,
            questions: questions, adaptiveDifficulty: nil
        )
    }

    /// Placeholder until section 1 is scored; replaced by the routed second section.
    private static func secondSectionPlaceholder(_ measure: GREMeasure, excluding: Set<String>) -> ExamSection {
        let questions = measure == .verbal
            ? FormBuilder.verbalSection(count: 15, level: .medium, excluding: excluding, remember: false)
            : FormBuilder.quantSection(count: 15, level: .medium, excluding: excluding, remember: false)
        return ExamSection(
            id: "\(measure.rawValue)-2-pending", measure: measure, ordinal: 2,
            durationSeconds: measure == .verbal ? 23 * 60 : 26 * 60,
            questions: questions, adaptiveDifficulty: .medium
        )
    }

    private static func writingSection() -> ExamSection {
        let question = QuestionBank.writingQuestions.randomElement() ?? QuestionBank.writingQuestion
        return ExamSection(id: "writing-1", measure: .analyticalWriting, ordinal: 1, durationSeconds: 30 * 60, questions: [question], adaptiveDifficulty: nil)
    }

    private static func makeSections(mode: ExamMode) -> [ExamSection] {
        func measure(_ m: GREMeasure) -> [ExamSection] {
            let first = firstSection(m)
            return [first, secondSectionPlaceholder(m, excluding: Set(first.questions.map(\.id)))]
        }
        switch mode {
        case .fullLength:
            let verbal = measure(.verbal), quant = measure(.quantitative)
            let ordered = FormBuilder.sectionOrder().map { pair in
                (pair.0 == .verbal ? verbal : quant)[pair.1 - 1]
            }
            return [writingSection()] + ordered
        case .verbalPractice:
            return measure(.verbal)
        case .quantitativePractice:
            return measure(.quantitative)
        case .writingPractice:
            return [writingSection()]
        }
    }

    #if DEBUG
    func completeWithCorrectAnswersForTesting() {
        var safetyCounter = 0
        while phase != .completed, safetyCounter < 10 {
            safetyCounter += 1
            if phase == .directions { beginSection() }
            for question in currentSection.questions {
                encounteredIDs.insert(question.id)
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
        assert(result?.questionReviews?.count == (result?.verbalTotal ?? 0) + (result?.quantitativeTotal ?? 0))
    }
    #endif
}

enum QuestionStatus {
    case notEncountered
    case notAnswered
    case incomplete
    case answered

    var title: String {
        switch self {
        case .notEncountered: "Not Encountered"
        case .notAnswered: "Not Answered"
        case .incomplete: "Incomplete"
        case .answered: "Answered"
        }
    }
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

/// Raw-to-scaled conversion estimate for the 27-question measures.
///
/// ETS converts the number correct across both sections, taking the second section's difficulty into
/// account, through equating; no conversion table is published for the computer test. This estimate
/// interpolates the official paper-test conversion (35 raw points) and weights the second section by
/// route difficulty, so only a fully correct harder path reaches 170.
enum ScoreEstimator {
    private static let verbalTable: [(Double, Double)] = [(0, 130), (2, 130), (5, 136), (10, 144), (15, 149), (20, 154), (25, 159), (30, 165), (35, 170)]
    private static let quantTable: [(Double, Double)] = [(0, 130), (2, 130), (5, 134), (10, 141), (15, 146), (20, 150), (25, 155), (30, 162), (35, 170)]

    static func scaled(measure: GREMeasure, firstCorrect: Int, firstTotal: Int, secondCorrect: Int, secondTotal: Int, route: QuestionDifficulty?) -> Int {
        let weight: Double = switch route {
        case .hard: 1.0
        case .medium: 0.8
        case .easy: 0.45
        case nil: 1.0
        }
        let maximum = Double(firstTotal) + Double(secondTotal)
        guard maximum > 0 else { return 130 }
        let points = Double(firstCorrect) + Double(secondCorrect) * weight
        let raw35 = points / maximum * 35
        let table = measure == .verbal ? verbalTable : quantTable
        var scaled = 130.0
        for (lower, upper) in zip(table, table.dropFirst()) where raw35 >= lower.0 {
            let fraction = min(1, (raw35 - lower.0) / (upper.0 - lower.0))
            scaled = lower.1 + fraction * (upper.1 - lower.1)
        }
        return min(170, max(130, Int(scaled.rounded())))
    }

    /// Percentile ranks (ETS GRE Table 1A, test takers July 2022 – June 2025).
    static func percentile(_ score: Int, measure: GREMeasure) -> Int? {
        let verbal = [170: 99, 169: 99, 168: 98, 167: 97, 166: 96, 165: 95, 164: 93, 163: 90, 162: 88, 161: 85, 160: 82, 159: 79, 158: 76, 157: 72, 156: 68, 155: 64, 154: 59, 153: 54, 152: 48, 151: 43, 150: 39, 149: 34, 148: 30, 147: 27, 146: 24, 145: 21, 144: 18, 143: 16, 142: 14, 141: 11, 140: 10, 139: 8, 138: 6, 137: 5, 136: 4, 135: 3, 134: 2, 133: 2, 132: 1, 131: 1]
        let quant = [170: 89, 169: 85, 168: 80, 167: 75, 166: 72, 165: 67, 164: 63, 163: 60, 162: 57, 161: 53, 160: 50, 159: 47, 158: 45, 157: 42, 156: 39, 155: 37, 154: 34, 153: 31, 152: 29, 151: 26, 150: 23, 149: 21, 148: 19, 147: 16, 146: 14, 145: 12, 144: 10, 143: 9, 142: 7, 141: 6, 140: 5, 139: 4, 138: 3, 137: 2, 136: 2, 135: 1, 134: 1, 133: 1]
        return (measure == .verbal ? verbal : quant)[score]
    }

    static func writingPercentile(_ score: Double) -> Int? {
        [6.0: 99, 5.5: 98, 5.0: 93, 4.5: 85, 4.0: 63, 3.5: 40, 3.0: 16, 2.5: 7, 2.0: 3, 1.5: 1, 1.0: 1][score]
    }
}
