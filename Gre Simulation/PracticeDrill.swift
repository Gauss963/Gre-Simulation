import Combine
import Foundation

/// The question types of the one-at-a-time practice pages: seven Verbal and four Quant.
enum DrillType: String, CaseIterable, Identifiable, Codable {
    case textCompletion1
    case textCompletion2
    case textCompletion3
    case sentenceEquivalence
    case readingSingle
    case readingMultiple
    case readingSelectSentence
    case quantitativeComparison
    case quantSingle
    case quantMultiple
    case numericEntry

    var id: String { rawValue }

    var measure: GREMeasure {
        switch self {
        case .quantitativeComparison, .quantSingle, .quantMultiple, .numericEntry: .quantitative
        default: .verbal
        }
    }

    static func types(for measure: GREMeasure) -> [DrillType] { allCases.filter { $0.measure == measure } }

    var title: String {
        switch self {
        case .textCompletion1: "Text Completion · 1 blank"
        case .textCompletion2: "Text Completion · 2 blanks"
        case .textCompletion3: "Text Completion · 3 blanks"
        case .sentenceEquivalence: "Sentence Equivalence"
        case .readingSingle: "Reading · select one"
        case .readingMultiple: "Reading · select all that apply"
        case .readingSelectSentence: "Reading · select a sentence"
        case .quantitativeComparison: "Quantitative Comparison"
        case .quantSingle: "Multiple choice · select one"
        case .quantMultiple: "Multiple choice · select one or more"
        case .numericEntry: "Numeric Entry"
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
        case .quantitativeComparison: "QC 比較"
        case .quantSingle: "單選"
        case .quantMultiple: "多選"
        case .numericEntry: "填數字"
        }
    }

    /// A one-line strategy reminder, shown after a wrong answer.
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
        case .quantitativeComparison:
            "先化簡兩邊再比較；代入 0、1、負數、分數、很大的數測試，只要出現兩種不同結果就選 D（無法判斷）。"
        case .quantSingle:
            "先估算刪掉不可能的選項；卡住時把選項代回題目，或代入簡單的數字驗算。"
        case .quantMultiple:
            "每個選項都要逐一檢查，正確的可能不只一個；先找出範圍的上下限，再判斷哪些選項落在範圍內。"
        case .numericEntry:
            "沒有選項可以猜，算完要檢查單位、四捨五入的位數與題目問的量；分數答案不必約分。"
        }
    }

    static func of(_ question: GREQuestion) -> DrillType? {
        switch (question.measure, question.format) {
        case (.verbal, .textCompletion):
            switch question.groups.count {
            case 1: return .textCompletion1
            case 2: return .textCompletion2
            default: return .textCompletion3
            }
        case (.verbal, .sentenceEquivalence): return .sentenceEquivalence
        case (.verbal, .singleChoice): return .readingSingle
        case (.verbal, .multipleChoice): return .readingMultiple
        case (.verbal, .selectInPassage): return .readingSelectSentence
        case (.quantitative, .quantitativeComparison): return .quantitativeComparison
        case (.quantitative, .singleChoice): return .quantSingle
        case (.quantitative, .multipleChoice): return .quantMultiple
        case (.quantitative, .numericEntry): return .numericEntry
        default: return nil
        }
    }
}

/// What accuracy is tracked by: the question type for Verbal, the content area for Quant.
struct DrillCategory: Hashable, Identifiable {
    let id: String
    let title: String
    let tip: String

    static let quantAreas: [DrillCategory] = [
        DrillCategory(id: "Arithmetic", title: "Arithmetic 算術",
                      tip: "整數性質、因數倍數、百分比與比例最常考：先確認是不是整數、會不會是負數或 0，百分比要看清楚是「誰的」百分之幾。"),
        DrillCategory(id: "Algebra", title: "Algebra 代數",
                      tip: "列式後先化簡再解；不等式乘除負數要變號，選項可以直接代回驗證。"),
        DrillCategory(id: "Geometry", title: "Geometry 幾何",
                      tip: "圖形不一定按比例，只相信標示的數字與條件；把已知角度、邊長標在圖上，找出直角三角形或相似三角形。"),
        DrillCategory(id: "Data Analysis", title: "Data Analysis 資料分析",
                      tip: "先讀清楚圖表標題、單位與總數；平均數、中位數、機率與排列組合各有固定公式，計算前先估算大概答案。"),
    ]

    /// The four official Quant content areas; narrower tags in the imported items are folded in.
    static func quantArea(of question: GREQuestion) -> String {
        switch question.contentArea {
        case "Algebra": "Algebra"
        case "Geometry": "Geometry"
        case "Data Analysis", "Probability", "Combinatorics": "Data Analysis"
        default: "Arithmetic"
        }
    }

    static func of(_ question: GREQuestion) -> DrillCategory? {
        if question.measure == .quantitative {
            let area = quantArea(of: question)
            return quantAreas.first { $0.id == area }
        }
        return DrillType.of(question).map { DrillCategory(id: $0.rawValue, title: $0.shortTitle, tip: $0.tip) }
    }

    static func all(for measure: GREMeasure) -> [DrillCategory] {
        measure == .quantitative
            ? quantAreas
            : DrillType.types(for: measure).map { DrillCategory(id: $0.rawValue, title: $0.shortTitle, tip: $0.tip) }
    }
}

enum PracticeDrill {
    static let verbalPool: [GREQuestion] = QuestionBank.all.filter { DrillType.of($0)?.measure == .verbal }
    static let quantPool: [GREQuestion] = QuestionBank.all.filter { DrillType.of($0)?.measure == .quantitative }

    static func pool(for measure: GREMeasure) -> [GREQuestion] { measure == .quantitative ? quantPool : verbalPool }

    static let questionsByID: [String: GREQuestion] =
        Dictionary((verbalPool + quantPool).map { ($0.id, $0) }, uniquingKeysWith: { first, _ in first })

    /// Every question of a passage or data set, in bank order.
    static let setMembers: [String: [GREQuestion]] =
        Dictionary(grouping: (verbalPool + quantPool).filter { $0.setID != nil || $0.stimulus != nil }, by: \.setKey)

    static func isComplete(_ answer: GREAnswer, for question: GREQuestion) -> Bool {
        if question.format == .numericEntry {
            let parts = answer.numericEntry.split(separator: "/", omittingEmptySubsequences: false)
            return !answer.numericEntry.isEmpty && !parts.contains { $0.trimmingCharacters(in: .whitespaces).isEmpty || $0 == "-" || $0 == "." }
        }
        return question.groups.allSatisfy { group in
            let count = answer.selections[group.id]?.count ?? 0
            switch question.format {
            case .sentenceEquivalence: return count == 2
            case .multipleChoice: return count >= 1
            default: return count == 1
            }
        }
    }

    static func isCorrect(_ answer: GREAnswer, for question: GREQuestion) -> Bool {
        if question.format == .numericEntry {
            return question.acceptedNumericAnswers.contains { NumericAnswerMatcher.matches(answer.numericEntry, $0) }
        }
        return question.correctSelections.allSatisfy { groupID, expected in (answer.selections[groupID] ?? []) == expected }
    }

    /// Your answer as text, blank by blank.
    static func answerText(_ answer: GREAnswer, for question: GREQuestion) -> String {
        if question.format == .numericEntry { return answer.numericEntry.isEmpty ? "No answer" : answer.numericEntry }
        return selectionText(answer.selections, for: question)
    }

    static func correctAnswerText(for question: GREQuestion) -> String {
        if question.format == .numericEntry { return question.acceptedNumericAnswers.sorted().joined(separator: " or ") }
        return selectionText(question.correctSelections, for: question)
    }

    private static func selectionText(_ selections: [String: Set<String>], for question: GREQuestion) -> String {
        let parts = question.groups.compactMap { group -> String? in
            let chosen = group.options.filter { selections[group.id]?.contains($0.id) == true }.map(\.text)
            guard !chosen.isEmpty else { return nil }
            return question.format == .selectInPassage ? chosen.map { "“\($0)”" }.joined() : chosen.joined(separator: ", ")
        }
        return parts.isEmpty ? "No answer" : parts.joined(separator: " · ")
    }

    static func completionHint(_ question: GREQuestion) -> String {
        switch question.format {
        case .sentenceEquivalence: "Select exactly two answer choices."
        case .textCompletion where question.groups.count > 1: "Select one entry for each blank."
        case .multipleChoice: "Select every answer choice that applies."
        case .selectInPassage: "Click the sentence in the passage."
        case .numericEntry: isFractionEntry(question) ? "Enter a numerator and a denominator." : "Enter your answer."
        default: "Select one answer choice."
        }
    }

    /// Fraction answers use two boxes (numerator over denominator).
    static func isFractionEntry(_ question: GREQuestion) -> Bool {
        !question.acceptedNumericAnswers.isEmpty && question.acceptedNumericAnswers.allSatisfy { $0.contains("/") }
    }
}

/// Saved practice attempts for one measure (one record per checked question), with accuracy by
/// category and the choice of the next question.
@MainActor
final class PracticeDrillStore: ObservableObject {
    struct Record: Codable, Identifiable, Equatable {
        let id: UUID
        let questionID: String
        let date: Date
        let selections: [String: [String]]
        var numericEntry: String? = nil
        let isCorrect: Bool

        var question: GREQuestion? { PracticeDrill.questionsByID[questionID] }
        var answer: GREAnswer { GREAnswer(selections: selections.mapValues(Set.init), numericEntry: numericEntry ?? "") }
    }

    struct CategoryAccuracy: Identifiable {
        let category: DrillCategory
        let correct: Int
        let total: Int
        var id: String { category.id }
        var accuracy: Double { total == 0 ? 0 : Double(correct) / Double(total) }
    }

    static func storageKey(for measure: GREMeasure) -> String {
        measure == .quantitative ? "gre-simulation.quant-drill.v1" : "gre-simulation.verbal-drill.v1"
    }

    static let recordLimit = 5_000

    let measure: GREMeasure
    @Published private(set) var records: [Record]
    private let defaults: UserDefaults

    init(measure: GREMeasure, defaults: UserDefaults = .standard) {
        self.measure = measure
        self.defaults = defaults
        if let data = defaults.data(forKey: Self.storageKey(for: measure)), let decoded = try? JSONDecoder().decode([Record].self, from: data) {
            records = decoded
        } else {
            records = []
        }
    }

    var pool: [GREQuestion] { PracticeDrill.pool(for: measure) }

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

    /// Accuracy per category from each question's most recent attempt.
    var accuracyByCategory: [CategoryAccuracy] {
        var sums: [String: (Int, Int)] = [:]
        for record in latestByQuestion.values {
            guard let question = record.question, let category = DrillCategory.of(question) else { continue }
            let current = sums[category.id] ?? (0, 0)
            sums[category.id] = (current.0 + (record.isCorrect ? 1 : 0), current.1 + 1)
        }
        return DrillCategory.all(for: measure).compactMap { category in
            sums[category.id].map { CategoryAccuracy(category: category, correct: $0.0, total: $0.1) }
        }
    }

    /// The two lowest-accuracy categories with at least five answered questions.
    var weakCategories: [DrillCategory] {
        Array(accuracyByCategory.filter { $0.total >= 5 }.sorted { $0.accuracy < $1.accuracy }.prefix(2).map(\.category))
    }

    func lastResult(for question: GREQuestion) -> Record? { latestByQuestion[question.id] }

    func record(_ question: GREQuestion, answer: GREAnswer) -> Bool {
        let correct = PracticeDrill.isCorrect(answer, for: question)
        records.append(Record(
            id: UUID(), questionID: question.id, date: .now,
            selections: answer.selections.mapValues { $0.sorted() },
            numericEntry: question.format == .numericEntry ? answer.numericEntry : nil,
            isCorrect: correct
        ))
        if records.count > Self.recordLimit { records.removeFirst(records.count - Self.recordLimit) }
        if let data = try? JSONEncoder().encode(records) { defaults.set(data, forKey: Self.storageKey(for: measure)) }
        // Practice tests then prefer questions you have not just seen here.
        QuestionBank.rememberExposure([question], for: measure)
        return correct
    }

    /// The questions matching the filters; `missedOnly` keeps those whose latest attempt was wrong.
    func candidates(types: Set<DrillType>?, area: String?, difficulty: QuestionDifficulty?, missedOnly: Bool) -> [GREQuestion] {
        let latest = latestByQuestion
        return pool.filter { question in
            guard let type = DrillType.of(question) else { return false }
            return (types == nil || types!.contains(type))
                && (area == nil || DrillCategory.quantArea(of: question) == area)
                && (difficulty == nil || question.difficulty == difficulty)
                && (!missedOnly || latest[question.id]?.isCorrect == false)
        }
    }

    /// The next question: the rest of the current passage or data set first; then unpracticed questions
    /// (the weakest categories first when `focus` is set), starting a set at its first unpracticed
    /// question; then missed questions, oldest first; then the least recently practiced.
    func next(
        after current: GREQuestion?,
        types: Set<DrillType>?,
        area: String? = nil,
        difficulty: QuestionDifficulty?,
        focus: Set<String>?,
        missedOnly: Bool
    ) -> GREQuestion? {
        let pool = candidates(types: types, area: area, difficulty: difficulty, missedOnly: missedOnly)
            .filter { $0.id != current?.id }
        guard !pool.isEmpty else { return nil }
        let latest = latestByQuestion
        let poolIDs = Set(pool.map(\.id))
        let isOpen: (GREQuestion) -> Bool = { missedOnly || latest[$0.id] == nil }

        if let current, let members = PracticeDrill.setMembers[current.setKey],
           let index = members.firstIndex(where: { $0.id == current.id }),
           let following = members[(index + 1)...].first(where: { poolIDs.contains($0.id) && isOpen($0) }) {
            return following
        }

        let open = pool.filter(isOpen)
        if !open.isEmpty {
            let preferred = focus.flatMap { focus in
                let matching = open.filter { DrillCategory.of($0).map { focus.contains($0.id) } == true }
                return matching.isEmpty ? nil : matching
            } ?? open
            guard let pick = preferred.randomElement() else { return nil }
            if let members = PracticeDrill.setMembers[pick.setKey],
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
