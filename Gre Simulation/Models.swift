import Foundation
import Combine

enum GREMeasure: String, Codable, CaseIterable, Identifiable {
    case analyticalWriting
    case verbal
    case quantitative

    var id: String { rawValue }

    var title: String {
        switch self {
        case .analyticalWriting: "Analytical Writing"
        case .verbal: "Verbal Reasoning"
        case .quantitative: "Quantitative Reasoning"
        }
    }

    var shortTitle: String {
        switch self {
        case .analyticalWriting: "Writing"
        case .verbal: "Verbal"
        case .quantitative: "Quant"
        }
    }
}

enum QuestionDifficulty: String, Codable, CaseIterable {
    case easy
    case medium
    case hard

    var title: String {
        switch self {
        case .easy: "Easy"
        case .medium: "Medium"
        case .hard: "Hard"
        }
    }
}

enum QuestionFormat: String, Codable {
    case singleChoice
    case multipleChoice
    case textCompletion
    case sentenceEquivalence
    case numericEntry
    case quantitativeComparison
    case essay

    var directions: String {
        switch self {
        case .singleChoice: "Select one answer choice."
        case .multipleChoice: "Select one or more answer choices, as directed."
        case .textCompletion: "For each blank, select one entry from the corresponding column of choices."
        case .sentenceEquivalence: "Select the two answer choices that, when used to complete the sentence, fit the meaning of the sentence as a whole and produce completed sentences that are alike in meaning."
        case .numericEntry: "Enter your answer as an integer or a decimal."
        case .quantitativeComparison: "Compare Quantity A and Quantity B, using additional information centered above the two quantities if it is given."
        case .essay: "Write a response in which you discuss the extent to which you agree or disagree with the statement and explain your reasoning."
        }
    }
}

struct ChoiceOption: Identifiable, Hashable {
    let id: String
    let text: String
}

struct ChoiceGroup: Identifiable, Hashable {
    let id: String
    let title: String?
    let options: [ChoiceOption]
    let maximumSelections: Int
}

struct GREQuestion: Identifiable {
    let id: String
    let measure: GREMeasure
    let difficulty: QuestionDifficulty
    let format: QuestionFormat
    let stimulus: String?
    let prompt: String
    let quantityA: String?
    let quantityB: String?
    let groups: [ChoiceGroup]
    let correctSelections: [String: Set<String>]
    let acceptedNumericAnswers: Set<String>
    let explanation: String
    let contentArea: String

    var isScored: Bool { format != .essay }
}

struct GREAnswer: Codable, Equatable {
    var selections: [String: Set<String>] = [:]
    var numericEntry = ""
    var essayText = ""

    var isEmpty: Bool {
        selections.values.allSatisfy(\.isEmpty)
            && numericEntry.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            && essayText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}

enum ExamMode: String, Codable, CaseIterable, Identifiable {
    case fullLength
    case verbalPractice
    case quantitativePractice

    var id: String { rawValue }

    var title: String {
        switch self {
        case .fullLength: "Full-length Practice Test"
        case .verbalPractice: "Verbal Timed Practice"
        case .quantitativePractice: "Quant Timed Practice"
        }
    }

    var subtitle: String {
        switch self {
        case .fullLength: "5 sections · approximately 1 hr 58 min"
        case .verbalPractice: "12 questions · 18 minutes"
        case .quantitativePractice: "12 questions · 21 minutes"
        }
    }
}

struct ExamSection: Identifiable {
    let id: String
    let measure: GREMeasure
    let ordinal: Int
    let durationSeconds: Int
    let questions: [GREQuestion]
    let adaptiveDifficulty: QuestionDifficulty?

    var displayTitle: String {
        if measure == .analyticalWriting { return measure.title }
        return "\(measure.title) · Section \(ordinal)"
    }
}

struct SectionSummary: Codable, Equatable {
    let measure: GREMeasure
    let ordinal: Int
    let difficulty: QuestionDifficulty?
    let correct: Int
    let total: Int
    let answered: Int
}

struct ExamResult: Identifiable, Codable, Equatable {
    let id: UUID
    let completedAt: Date
    let mode: ExamMode
    let verbalScore: Int?
    let quantitativeScore: Int?
    let writingEstimate: Double?
    let verbalCorrect: Int
    let verbalTotal: Int
    let quantitativeCorrect: Int
    let quantitativeTotal: Int
    let elapsedSeconds: Int
    let sections: [SectionSummary]

    var combinedScore: Int? {
        guard let verbalScore, let quantitativeScore else { return nil }
        return verbalScore + quantitativeScore
    }
}

struct VocabularyWord: Identifiable, Hashable {
    let word: String
    let pronunciation: String
    let definition: String
    let chinese: String
    let synonyms: [String]
    let example: String

    var id: String { word }
}

@MainActor
final class HistoryStore: ObservableObject {
    @Published private(set) var results: [ExamResult] = []

    private let storageKey = "gre-simulation.history.v1"

    init() {
        guard let data = UserDefaults.standard.data(forKey: storageKey),
              let decoded = try? JSONDecoder().decode([ExamResult].self, from: data) else { return }
        results = decoded.sorted { $0.completedAt > $1.completedAt }
    }

    func add(_ result: ExamResult) {
        results.insert(result, at: 0)
        save()
    }

    func clear() {
        results.removeAll()
        save()
    }

    private func save() {
        guard let data = try? JSONEncoder().encode(results) else { return }
        UserDefaults.standard.set(data, forKey: storageKey)
    }
}
