import Foundation

/// Builds sections that follow the current GRE General Test.
///
/// ETS: Analytical Writing is always first; the four Verbal and Quantitative sections follow in any
/// order (each measure's first section before its second). Verbal: about half Reading Comprehension,
/// with passages kept together; Text Completion first, then reading, Sentence Equivalence, and more
/// reading (official practice test ordering). Quant: Quantitative Comparison first (about a third),
/// then discrete questions and one Data Interpretation set. Section 1 is of average difficulty; the
/// second section's difficulty depends on section 1 performance.
enum FormBuilder {
    enum Level { case average, easy, medium, hard }

    // MARK: - Pools

    private static func isOfficialShape(_ question: GREQuestion) -> Bool {
        let options = question.groups.first?.options.count ?? 0
        switch (question.measure, question.format) {
        case (.verbal, .singleChoice): return options == 5
        case (.verbal, .multipleChoice): return options == 3
        case (.verbal, .sentenceEquivalence): return options == 6
        case (.quantitative, .singleChoice): return options == 5
        case (.quantitative, .multipleChoice): return options >= 3
        default: return true
        }
    }

    private static let verbalPool = QuestionBank.all.filter { $0.measure == .verbal && isOfficialShape($0) }
    private static let quantPool = QuestionBank.all.filter { $0.measure == .quantitative && isOfficialShape($0) }

    private static func isReading(_ q: GREQuestion) -> Bool {
        q.stimulus != nil && [.singleChoice, .multipleChoice, .selectInPassage].contains(q.format)
    }

    /// Reading sets: questions sharing a passage, in authored order.
    private static let readingSets: [[GREQuestion]] = {
        Dictionary(grouping: verbalPool.filter(isReading), by: \.setKey).values.map { Array($0) }
    }()

    /// Data Interpretation sets: questions sharing one data display (multiple choice or numeric entry).
    private static let dataSets: [[GREQuestion]] = {
        let candidates = quantPool.filter {
            $0.figure != nil && [.singleChoice, .multipleChoice, .numericEntry].contains($0.format)
        }
        return Dictionary(grouping: candidates) { "\($0.stimulus ?? "")|\($0.figure?.title ?? "")" }
            .values.filter { $0.count >= 2 }.map { Array($0) }
    }()

    // MARK: - Difficulty

    private static func weight(_ difficulty: QuestionDifficulty, for level: Level) -> [QuestionDifficulty] {
        switch level {
        case .average: [.medium, .easy, .hard]
        case .easy: [.easy, .medium, .hard]
        case .medium: [.medium, .easy, .hard]
        case .hard: [.hard, .medium, .easy]
        }
    }

    /// Target difficulty for each slot: section 1 mixes levels around average; section 2 leans to its route.
    private static func targets(count: Int, level: Level) -> [QuestionDifficulty] {
        let pattern: [QuestionDifficulty] = switch level {
        case .average: [.easy, .medium, .medium, .hard]
        case .easy: [.easy, .easy, .medium]
        case .medium: [.medium, .medium, .easy, .hard]
        case .hard: [.hard, .hard, .medium]
        }
        return (0..<count).map { pattern[$0 % pattern.count] }
    }

    // MARK: - Sections

    static func verbalSection(count: Int, level: Level, excluding: Set<String>, remember: Bool = true) -> [GREQuestion] {
        var used = excluding
        var picker = Picker(level: level, measure: .verbal)
        let tcBlanks = count == 12 ? [1, 2, 2, 3] : [1, 1, 2, 2, 3]
        let seCount = count == 12 ? 2 : 3
        let firstReading = 2
        let laterReading = count - tcBlanks.count - seCount - firstReading

        var questions: [GREQuestion] = []
        for (slot, blanks) in tcBlanks.enumerated() {
            if let q = picker.pickDiscrete(from: verbalPool.filter { $0.format == .textCompletion && $0.groups.count == blanks }, slot: slot, used: &used) {
                questions.append(q)
            }
        }
        questions += picker.pickReading(total: firstReading, sets: readingSets, longAllowed: false, used: &used)
        for slot in 0..<seCount {
            if let q = picker.pickDiscrete(from: verbalPool.filter { $0.format == .sentenceEquivalence }, slot: slot + 4, used: &used) {
                questions.append(q)
            }
        }
        questions += picker.pickReading(total: laterReading, sets: readingSets, longAllowed: count == 15, used: &used)
        if remember { QuestionBank.rememberExposure(questions, for: .verbal) }
        return questions
    }

    static func quantSection(count: Int, level: Level, excluding: Set<String>, remember: Bool = true) -> [GREQuestion] {
        var used = excluding
        var picker = Picker(level: level, measure: .quantitative)
        let qcCount = count == 12 ? 4 : 5
        let dataCount = count == 12 ? 2 : 3
        let discrete: [QuestionFormat] = count == 12
            ? [.singleChoice, .singleChoice, .multipleChoice, .numericEntry, .singleChoice, .numericEntry]
            : [.singleChoice, .singleChoice, .multipleChoice, .numericEntry, .singleChoice, .numericEntry, .singleChoice]

        var questions: [GREQuestion] = []
        for slot in 0..<qcCount {
            if let q = picker.pickDiscrete(from: quantPool.filter { $0.format == .quantitativeComparison }, slot: slot, used: &used) {
                questions.append(q)
            }
        }
        var rest: [GREQuestion] = []
        for (slot, format) in discrete.enumerated() {
            let pool = quantPool.filter { $0.format == format && !($0.figure != nil && format != .quantitativeComparison && $0.stimulus != nil) }
            if let q = picker.pickDiscrete(from: pool, slot: slot + qcCount, used: &used) { rest.append(q) }
        }
        let data = picker.pickDataSet(size: dataCount, sets: dataSets, used: &used)
        // The Data Interpretation set sits among the later discrete questions, as on official forms.
        let insertAt = min(rest.count, max(1, rest.count - 2))
        rest.insert(contentsOf: data, at: insertAt)
        questions += rest
        if remember { QuestionBank.rememberExposure(questions, for: .quantitative) }
        return questions
    }

    /// Analytical Writing first, then the four measure sections in a random valid order.
    static func sectionOrder() -> [(GREMeasure, Int)] {
        let orders: [[(GREMeasure, Int)]] = [
            [(.verbal, 1), (.quantitative, 1), (.verbal, 2), (.quantitative, 2)],
            [(.quantitative, 1), (.verbal, 1), (.quantitative, 2), (.verbal, 2)],
            [(.verbal, 1), (.verbal, 2), (.quantitative, 1), (.quantitative, 2)],
            [(.quantitative, 1), (.quantitative, 2), (.verbal, 1), (.verbal, 2)],
            [(.verbal, 1), (.quantitative, 1), (.quantitative, 2), (.verbal, 2)],
            [(.quantitative, 1), (.verbal, 1), (.verbal, 2), (.quantitative, 2)]
        ]
        return orders.randomElement() ?? orders[0]
    }

    // MARK: - Picking

    private struct Picker {
        let level: Level
        let measure: GREMeasure
        let recent: Set<String>

        init(level: Level, measure: GREMeasure) {
            self.level = level
            self.measure = measure
            self.recent = QuestionBank.recentQuestionIDs(for: measure)
        }

        private func rank(_ pool: [GREQuestion], target: QuestionDifficulty) -> [GREQuestion] {
            let order = [target] + FormBuilder.weight(target, for: level).filter { $0 != target }
            return order.flatMap { level in
                let tier = pool.filter { $0.difficulty == level }
                return tier.filter { !recent.contains($0.id) }.shuffled() + tier.filter { recent.contains($0.id) }.shuffled()
            }
        }

        mutating func pickDiscrete(from pool: [GREQuestion], slot: Int, used: inout Set<String>) -> GREQuestion? {
            let target = FormBuilder.targets(count: slot + 1, level: level)[slot]
            guard let choice = rank(pool.filter { !used.contains($0.id) }, target: target).first else { return nil }
            used.insert(choice.id)
            return choice
        }

        mutating func pickReading(total: Int, sets: [[GREQuestion]], longAllowed: Bool, used: inout Set<String>) -> [GREQuestion] {
            var picked: [GREQuestion] = []
            var usedLong = false
            while picked.count < total {
                let remaining = total - picked.count
                let target = FormBuilder.targets(count: picked.count + 1, level: level)[picked.count]
                let wantLong = longAllowed && !usedLong && remaining >= 3
                let candidates = sets.filter { set in
                    !set.contains { used.contains($0.id) }
                        && (wantLong ? isLong(set) : !isLong(set))
                }
                let ranked = candidates.sorted { _, _ in Bool.random() }.sorted {
                    distance($0[0].difficulty, target) < distance($1[0].difficulty, target)
                }
                guard let set = ranked.first ?? sets.first(where: { !$0.contains { used.contains($0.id) } }) else { break }
                let take = min(remaining, isLong(set) ? 4 : min(3, set.count))
                let chosen = Array(set.prefix(take))
                if isLong(set) { usedLong = true }
                picked += chosen
                for question in set { used.insert(question.id) }
            }
            return picked
        }

        mutating func pickDataSet(size: Int, sets: [[GREQuestion]], used: inout Set<String>) -> [GREQuestion] {
            let target: QuestionDifficulty = level == .hard ? .hard : (level == .easy ? .easy : .medium)
            let candidates = sets.filter { $0.count >= size && !$0.contains { used.contains($0.id) } }
            let ranked = candidates.shuffled().sorted { lhs, rhs in
                let l = lhs.filter { $0.difficulty == target }.count, r = rhs.filter { $0.difficulty == target }.count
                return l > r
            }
            guard let set = ranked.first else { return [] }
            let chosen = Array(set.shuffled().prefix(size))
            for question in set { used.insert(question.id) }
            return chosen
        }

        private func isLong(_ set: [GREQuestion]) -> Bool {
            (set.first?.stimulus?.split(separator: " ").count ?? 0) > 300
        }

        private func distance(_ a: QuestionDifficulty, _ b: QuestionDifficulty) -> Int {
            let order: [QuestionDifficulty: Int] = [.easy: 0, .medium: 1, .hard: 2]
            return abs((order[a] ?? 1) - (order[b] ?? 1))
        }
    }

    // MARK: - Validation

    static func validate() -> [String] {
        var issues: [String] = []
        for _ in 0..<20 {
            for level in [Level.average, .easy, .medium, .hard] {
                for count in [12, 15] {
                    let verbal = verbalSection(count: count, level: level, excluding: [], remember: false)
                    let quant = quantSection(count: count, level: level, excluding: [], remember: false)
                    if verbal.count != count { issues.append("Verbal \(count)-question section built \(verbal.count) questions.") }
                    if quant.count != count { issues.append("Quant \(count)-question section built \(quant.count) questions.") }
                    let reading = verbal.filter { $0.stimulus != nil }.count
                    if abs(reading * 2 - count) > 2 { issues.append("Verbal section has \(reading) reading questions of \(count).") }
                    if Set(verbal.map(\.id)).count != verbal.count || Set(quant.map(\.id)).count != quant.count {
                        issues.append("A section repeats a question.")
                    }
                }
            }
        }
        return Array(Set(issues)).sorted()
    }
}
