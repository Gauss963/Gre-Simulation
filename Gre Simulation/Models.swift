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

struct ChoiceOption: Identifiable, Hashable, Codable {
    let id: String
    let text: String
}

struct ChoiceGroup: Identifiable, Hashable, Codable {
    let id: String
    let title: String?
    let options: [ChoiceOption]
    let maximumSelections: Int
}

struct QuestionSource: Hashable, Codable {
    let title: String
    let detail: String
    let isAuthorizedSourceItem: Bool
}

enum QuantFigureKind: String, Codable {
    case table
    case bar
    case groupedBar
    case line
    case pie
    case scatter
    case histogram
    case boxPlot
    case normalCurve
    case venn
}

struct QuantFigurePoint: Hashable, Codable {
    let label: String?
    let x: Double?
    let value: Double?
    let low: Double?
    let q1: Double?
    let median: Double?
    let q3: Double?
    let high: Double?
}

struct QuantFigureSeries: Hashable, Codable, Identifiable {
    let name: String
    let points: [QuantFigurePoint]

    var id: String { name }
}

struct QuantFigureAnnotation: Hashable, Codable, Identifiable {
    let label: String
    let value: String
    let x: Double
    let y: Double

    var id: String { "\(label)-\(x)-\(y)" }
}

struct QuantFigure: Hashable, Codable {
    let kind: QuantFigureKind
    let title: String
    let caption: String?
    let xAxisTitle: String?
    let yAxisTitle: String?
    let headers: [String]?
    let rows: [[String]]?
    let series: [QuantFigureSeries]
    let annotations: [QuantFigureAnnotation]?
}

struct GREQuestion: Identifiable, Codable, Equatable {
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
    var source: QuestionSource? = nil
    var figure: QuantFigure? = nil

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

struct QuestionReviewRecord: Identifiable, Codable, Equatable {
    let sectionID: String
    let sectionOrdinal: Int
    let sectionDifficulty: QuestionDifficulty?
    let questionNumber: Int
    let question: GREQuestion
    let answer: GREAnswer
    let isCorrect: Bool

    var id: String { "\(sectionID)-\(question.id)" }
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
    let questionReviews: [QuestionReviewRecord]?

    init(
        id: UUID,
        completedAt: Date,
        mode: ExamMode,
        verbalScore: Int?,
        quantitativeScore: Int?,
        writingEstimate: Double?,
        verbalCorrect: Int,
        verbalTotal: Int,
        quantitativeCorrect: Int,
        quantitativeTotal: Int,
        elapsedSeconds: Int,
        sections: [SectionSummary],
        questionReviews: [QuestionReviewRecord]? = nil
    ) {
        self.id = id
        self.completedAt = completedAt
        self.mode = mode
        self.verbalScore = verbalScore
        self.quantitativeScore = quantitativeScore
        self.writingEstimate = writingEstimate
        self.verbalCorrect = verbalCorrect
        self.verbalTotal = verbalTotal
        self.quantitativeCorrect = quantitativeCorrect
        self.quantitativeTotal = quantitativeTotal
        self.elapsedSeconds = elapsedSeconds
        self.sections = sections
        self.questionReviews = questionReviews
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        completedAt = try container.decode(Date.self, forKey: .completedAt)
        mode = try container.decode(ExamMode.self, forKey: .mode)
        verbalScore = try container.decodeIfPresent(Int.self, forKey: .verbalScore)
        quantitativeScore = try container.decodeIfPresent(Int.self, forKey: .quantitativeScore)
        writingEstimate = try container.decodeIfPresent(Double.self, forKey: .writingEstimate)
        verbalCorrect = try container.decode(Int.self, forKey: .verbalCorrect)
        verbalTotal = try container.decode(Int.self, forKey: .verbalTotal)
        quantitativeCorrect = try container.decode(Int.self, forKey: .quantitativeCorrect)
        quantitativeTotal = try container.decode(Int.self, forKey: .quantitativeTotal)
        elapsedSeconds = try container.decode(Int.self, forKey: .elapsedSeconds)
        sections = try container.decode([SectionSummary].self, forKey: .sections)
        questionReviews = try container.decodeIfPresent([QuestionReviewRecord].self, forKey: .questionReviews)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(completedAt, forKey: .completedAt)
        try container.encode(mode, forKey: .mode)
        try container.encodeIfPresent(verbalScore, forKey: .verbalScore)
        try container.encodeIfPresent(quantitativeScore, forKey: .quantitativeScore)
        try container.encodeIfPresent(writingEstimate, forKey: .writingEstimate)
        try container.encode(verbalCorrect, forKey: .verbalCorrect)
        try container.encode(verbalTotal, forKey: .verbalTotal)
        try container.encode(quantitativeCorrect, forKey: .quantitativeCorrect)
        try container.encode(quantitativeTotal, forKey: .quantitativeTotal)
        try container.encode(elapsedSeconds, forKey: .elapsedSeconds)
        try container.encode(sections, forKey: .sections)
        try container.encodeIfPresent(questionReviews, forKey: .questionReviews)
    }

    private enum CodingKeys: String, CodingKey {
        case id, completedAt, mode, verbalScore, quantitativeScore, writingEstimate
        case verbalCorrect, verbalTotal, quantitativeCorrect, quantitativeTotal
        case elapsedSeconds, sections, questionReviews
    }

    var combinedScore: Int? {
        guard let verbalScore, let quantitativeScore else { return nil }
        return verbalScore + quantitativeScore
    }
}

struct VocabularyWord: Identifiable, Hashable, Codable {
    let word: String
    let pronunciation: String
    let definition: String
    let chinese: String
    let synonyms: [String]
    let example: String
    var exampleSource: String? = nil
    var exampleSourceURL: String? = nil
    var sources: [String] = []
    var isHighFrequency: Bool = false

    var id: String { word }
}

@MainActor
final class HistoryStore: ObservableObject {
    @Published private(set) var results: [ExamResult] = []

    private let legacyStorageKey = "gre-simulation.history.v1"
    private let storageURL: URL?

    init() {
        storageURL = Self.makeStorageURL()
        let fileData = storageURL.flatMap { try? Data(contentsOf: $0) }
        let legacyData = UserDefaults.standard.data(forKey: legacyStorageKey)
        let fileResults = fileData.flatMap { Self.decodeHistory($0, source: "application support") }
        let legacyResults = legacyData.flatMap { Self.decodeHistory($0, source: "legacy preferences") }
        guard fileResults != nil || legacyResults != nil else { return }
        var merged = Dictionary(uniqueKeysWithValues: (legacyResults ?? []).map { ($0.id, $0) })
        for result in fileResults ?? [] {
            merged[result.id] = result
        }
        results = merged.values.sorted { $0.completedAt > $1.completedAt }
        if legacyResults != nil, save() {
            UserDefaults.standard.removeObject(forKey: legacyStorageKey)
        }
    }

    func add(_ result: ExamResult) {
        results.insert(result, at: 0)
        save()
    }

    func clear() {
        results.removeAll()
        save()
        UserDefaults.standard.removeObject(forKey: legacyStorageKey)
    }

    @discardableResult
    private func save() -> Bool {
        guard let data = try? JSONEncoder().encode(results) else { return false }
        guard let storageURL else {
            UserDefaults.standard.set(data, forKey: legacyStorageKey)
            return true
        }
        do {
            try data.write(to: storageURL, options: .atomic)
            return true
        } catch {
            #if DEBUG
            print("Could not save score history: \(error)")
            #endif
            return false
        }
    }

    private static func makeStorageURL() -> URL? {
        guard let supportDirectory = FileManager.default.urls(
            for: .applicationSupportDirectory,
            in: .userDomainMask
        ).first else { return nil }
        let directory = supportDirectory.appendingPathComponent("Gre Simulation", isDirectory: true)
        do {
            try FileManager.default.createDirectory(
                at: directory,
                withIntermediateDirectories: true
            )
            return directory.appendingPathComponent("ScoreHistory-v2.json")
        } catch {
            #if DEBUG
            print("Could not prepare score-history storage: \(error)")
            #endif
            return nil
        }
    }

    private static func decodeHistory(_ data: Data, source: String) -> [ExamResult]? {
        do {
            return try JSONDecoder().decode([ExamResult].self, from: data)
        } catch {
            #if DEBUG
            print("Could not decode \(source) score history: \(error)")
            #endif
            return nil
        }
    }
}
