import Combine
import Foundation

/// The seven Verbal question types the practice page covers.
enum VerbalDrillType: String, CaseIterable, Identifiable, Codable {
    case textCompletion1
    case textCompletion2
    case textCompletion3
    case sentenceEquivalence
    case readingSingle
    case readingMultiple
    case readingSelectSentence

    var id: String { rawValue }

    var title: String {
        switch self {
        case .textCompletion1: "Text Completion · 1 blank"
        case .textCompletion2: "Text Completion · 2 blanks"
        case .textCompletion3: "Text Completion · 3 blanks"
        case .sentenceEquivalence: "Sentence Equivalence"
        case .readingSingle: "Reading · select one"
        case .readingMultiple: "Reading · select all that apply"
        case .readingSelectSentence: "Reading · select a sentence"
        }
    }

    var shortTitle: String {
        switch self {
        case .textCompletion1: "TC 1 空"
        case .textCompletion2: "TC 2 空"
        case .textCompletion3: "TC 3 空"
        case .sentenceEquivalence: "SE 同義句"
        case .readingSingle: "閱讀單選"
        case .readingMultiple: "閱讀多選"
        case .readingSelectSentence: "閱讀選句"
        }
    }

    /// A one-line strategy reminder, shown after a wrong answer and for the weakest type.
    var tip: String {
        switch self {
        case .textCompletion1:
            "先遮住選項，用 although、because、分號等訊號字決定空格方向，自己想一個簡單字，再找最接近的選項。"
        case .textCompletion2, .textCompletion3:
            "先做線索最明確的空格，再用它推其他空格；每空都要單獨成立，最後整句代回去讀一次。"
        case .sentenceEquivalence:
            "答案是兩個讓句意相同的字：先預測空格意思，再找成對的近義選項；落單的字、方向相反的同義對都要刪。"
        case .readingSingle:
            "先判斷題型（主旨、細節、推論、目的、態度），回原文找證據；過度極端、超出範圍、把說法顛倒的選項先刪。"
        case .readingMultiple:
            "每個選項都要在文中找到證據才選，沒有證據就不選；可能只有一個正確，也可能全部正確，全對才給分。"
        case .readingSelectSentence:
            "把題目要的功能（提出反例、轉折、證據）先想清楚，再逐句比對；答案常是 however、but 等轉折句。"
        }
    }

    static func of(_ question: GREQuestion) -> VerbalDrillType? {
        guard question.measure == .verbal else { return nil }
        switch question.format {
        case .textCompletion:
            switch question.groups.count {
            case 1: return .textCompletion1
            case 2: return .textCompletion2
            default: return .textCompletion3
            }
        case .sentenceEquivalence: return .sentenceEquivalence
        case .singleChoice: return .readingSingle
        case .multipleChoice: return .readingMultiple
        case .selectInPassage: return .readingSelectSentence
        default: return nil
        }
    }
}

enum VerbalDrill {
    /// Every deliverable Verbal question (retired items are already excluded from the bank).
    static let pool: [GREQuestion] = QuestionBank.all.filter { VerbalDrillType.of($0) != nil }

    static let questionsByID: [String: GREQuestion] =
        Dictionary(pool.map { ($0.id, $0) }, uniquingKeysWith: { first, _ in first })

    /// Every question of a passage set, in bank order.
    static let setMembers: [String: [GREQuestion]] = Dictionary(grouping: pool.filter { $0.setID != nil || $0.stimulus != nil }, by: \.setKey)

    static func isComplete(_ answer: GREAnswer, for question: GREQuestion) -> Bool {
        question.groups.allSatisfy { group in
            let count = answer.selections[group.id]?.count ?? 0
            switch question.format {
            case .sentenceEquivalence: return count == 2
            case .multipleChoice: return count >= 1
            default: return count == 1
            }
        }
    }

    static func isCorrect(_ answer: GREAnswer, for question: GREQuestion) -> Bool {
        question.correctSelections.allSatisfy { groupID, expected in (answer.selections[groupID] ?? []) == expected }
    }

    /// The correct choices as text, blank by blank.
    static func answerText(_ selections: [String: Set<String>], for question: GREQuestion) -> String {
        let parts = question.groups.compactMap { group -> String? in
            let chosen = group.options.filter { selections[group.id]?.contains($0.id) == true }.map(\.text)
            guard !chosen.isEmpty else { return nil }
            return question.format == .selectInPassage ? chosen.map { "“\($0)”" }.joined() : chosen.joined(separator: ", ")
        }
        return parts.isEmpty ? "No answer" : parts.joined(separator: " · ")
    }
}

/// Saved Verbal practice attempts (one per checked question), with accuracy by type and the choice of
/// the next question.
@MainActor
final class VerbalDrillStore: ObservableObject {
    struct Record: Codable, Identifiable, Equatable {
        let id: UUID
        let questionID: String
        let date: Date
        let selections: [String: [String]]
        let isCorrect: Bool

        var question: GREQuestion? { VerbalDrill.questionsByID[questionID] }
        var answer: GREAnswer { GREAnswer(selections: selections.mapValues(Set.init)) }
    }

    struct TypeAccuracy: Identifiable {
        let type: VerbalDrillType
        let correct: Int
        let total: Int
        var id: String { type.rawValue }
        var accuracy: Double { total == 0 ? 0 : Double(correct) / Double(total) }
    }

    static let storageKey = "gre-simulation.verbal-drill.v1"
    static let recordLimit = 5_000

    @Published private(set) var records: [Record]
    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        if let data = defaults.data(forKey: Self.storageKey), let decoded = try? JSONDecoder().decode([Record].self, from: data) {
            records = decoded
        } else {
            records = []
        }
    }

    /// Newest first; attempts on questions no longer in the bank are left out.
    var history: [Record] { records.reversed().filter { $0.question != nil } }

    var latestByQuestion: [String: Record] {
        var latest: [String: Record] = [:]
        for record in records where record.question != nil { latest[record.questionID] = record }
        return latest
    }

    var practicedCount: Int { latestByQuestion.count }

    /// Accuracy over each practiced question's most recent attempt.
    var recentAccuracy: Double? {
        let latest = latestByQuestion.values
        return latest.isEmpty ? nil : Double(latest.filter(\.isCorrect).count) / Double(latest.count)
    }

    var missedCount: Int { latestByQuestion.values.filter { !$0.isCorrect }.count }

    /// Accuracy per type from each question's most recent attempt.
    var accuracyByType: [TypeAccuracy] {
        var sums: [VerbalDrillType: (Int, Int)] = [:]
        for record in latestByQuestion.values {
            guard let question = record.question, let type = VerbalDrillType.of(question) else { continue }
            let current = sums[type] ?? (0, 0)
            sums[type] = (current.0 + (record.isCorrect ? 1 : 0), current.1 + 1)
        }
        return VerbalDrillType.allCases.compactMap { type in sums[type].map { TypeAccuracy(type: type, correct: $0.0, total: $0.1) } }
    }

    /// The two lowest-accuracy types with at least five answered questions.
    var weakTypes: [VerbalDrillType] {
        Array(accuracyByType.filter { $0.total >= 5 }.sorted { $0.accuracy < $1.accuracy }.prefix(2).map(\.type))
    }

    func lastResult(for question: GREQuestion) -> Record? { latestByQuestion[question.id] }

    func record(_ question: GREQuestion, answer: GREAnswer) -> Bool {
        let correct = VerbalDrill.isCorrect(answer, for: question)
        records.append(Record(
            id: UUID(), questionID: question.id, date: .now,
            selections: answer.selections.mapValues { $0.sorted() }, isCorrect: correct
        ))
        if records.count > Self.recordLimit { records.removeFirst(records.count - Self.recordLimit) }
        if let data = try? JSONEncoder().encode(records) { defaults.set(data, forKey: Self.storageKey) }
        // Practice tests then prefer questions you have not just seen here.
        QuestionBank.rememberExposure([question], for: .verbal)
        return correct
    }

    /// The questions matching the filters; `missedOnly` keeps those whose latest attempt was wrong.
    func candidates(types: Set<VerbalDrillType>?, difficulty: QuestionDifficulty?, missedOnly: Bool) -> [GREQuestion] {
        let latest = latestByQuestion
        return VerbalDrill.pool.filter { question in
            guard let type = VerbalDrillType.of(question) else { return false }
            return (types == nil || types!.contains(type))
                && (difficulty == nil || question.difficulty == difficulty)
                && (!missedOnly || latest[question.id]?.isCorrect == false)
        }
    }

    /// The next question: the rest of the current passage first; then unpracticed questions (the
    /// weakest types first when `focus` is set), starting a passage at its first unpracticed
    /// question; then missed questions, oldest first; then the least recently practiced.
    func next(
        after current: GREQuestion?,
        types: Set<VerbalDrillType>?,
        difficulty: QuestionDifficulty?,
        focus: Set<VerbalDrillType>?,
        missedOnly: Bool
    ) -> GREQuestion? {
        let pool = candidates(types: types, difficulty: difficulty, missedOnly: missedOnly)
            .filter { $0.id != current?.id }
        guard !pool.isEmpty else { return nil }
        let latest = latestByQuestion
        let poolIDs = Set(pool.map(\.id))
        let isOpen: (GREQuestion) -> Bool = { missedOnly || latest[$0.id] == nil }

        if let current, let members = VerbalDrill.setMembers[current.setKey],
           let index = members.firstIndex(where: { $0.id == current.id }),
           let following = members[(index + 1)...].first(where: { poolIDs.contains($0.id) && isOpen($0) }) {
            return following
        }

        let open = pool.filter(isOpen)
        if !open.isEmpty {
            let preferred = focus.flatMap { focus in
                let matching = open.filter { VerbalDrillType.of($0).map(focus.contains) == true }
                return matching.isEmpty ? nil : matching
            } ?? open
            guard let pick = preferred.randomElement() else { return nil }
            if let members = VerbalDrill.setMembers[pick.setKey],
               let first = members.first(where: { poolIDs.contains($0.id) && isOpen($0) }) {
                return first
            }
            return pick
        }
        return pool.min { lhs, rhs in
            let a = latest[lhs.id], b = latest[rhs.id]
            let left = (a?.isCorrect == true ? 1 : 0, a?.date ?? .distantPast)
            let right = (b?.isCorrect == true ? 1 : 0, b?.date ?? .distantPast)
            return left < right
        }
    }
}
