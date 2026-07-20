import Foundation

enum QuestionBank {
    static let writingQuestion = GREQuestion(
        id: "aw-001",
        measure: .analyticalWriting,
        difficulty: .medium,
        format: .essay,
        stimulus: nil,
        prompt: "Educational institutions should encourage students to question established ideas rather than simply master them.",
        quantityA: nil,
        quantityB: nil,
        groups: [],
        correctSelections: [:],
        acceptedNumericAnswers: [],
        explanation: "A strong response defines the limits of the claim, develops a clear position, and supports it with relevant reasons and examples while addressing meaningful counterarguments.",
        contentArea: "Analyze an Issue"
    )

    static let expandedQuestions: [GREQuestion] =
        BundledResource.decode([GREQuestion].self, named: "ExpandedQuestions") ?? []

    static let authorized20260720Questions: [GREQuestion] =
        BundledResource.decode([GREQuestion].self, named: "Authorized20260720Questions") ?? []

    static let all: [GREQuestion] =
        verbalQuestions + quantitativeQuestions + expandedQuestions + authorized20260720Questions

    static let writingQuestions: [GREQuestion] =
        [writingQuestion] + authorized20260720Questions.filter { $0.measure == .analyticalWriting }

    static var sourceSummary: [(title: String, count: Int)] {
        let grouped = Dictionary(grouping: all) { question in
            question.source?.title ?? "Original app question bank"
        }
        return grouped.map { ($0.key, $0.value.count) }.sorted { $0.title < $1.title }
    }

    static func questions(
        for measure: GREMeasure,
        difficulty: QuestionDifficulty? = nil,
        count: Int,
        excluding excluded: Set<String> = []
    ) -> [GREQuestion] {
        let available = all.filter { $0.measure == measure && !excluded.contains($0.id) }
        #if DEBUG
        let arguments = ProcessInfo.processInfo.arguments
        if measure == .quantitative,
           let flagIndex = arguments.firstIndex(of: "-qaQuestion"),
           arguments.indices.contains(flagIndex + 1),
           let preferred = available.first(where: { $0.id == arguments[flagIndex + 1] }) {
            let remainder = available.filter { $0.id != preferred.id }.shuffled()
            return Array(([preferred] + remainder).prefix(count))
        }
        #endif
        guard let difficulty else {
            let each = max(1, count / 3)
            var selected: [GREQuestion] = []
            for level in QuestionDifficulty.allCases {
                selected.append(contentsOf: available.filter { $0.difficulty == level }.shuffled().prefix(each))
            }
            if selected.count < count {
                let used = Set(selected.map(\.id))
                selected.append(contentsOf: available.filter { !used.contains($0.id) }.shuffled().prefix(count - selected.count))
            }
            return Array(selected.shuffled().prefix(count))
        }

        var selected = Array(available.filter { $0.difficulty == difficulty }.shuffled().prefix(count))
        if selected.count < count {
            let used = Set(selected.map(\.id))
            let fallback = available
                .filter { !used.contains($0.id) }
                .sorted { distance($0.difficulty, from: difficulty) < distance($1.difficulty, from: difficulty) }
            selected.append(contentsOf: fallback.prefix(count - selected.count))
        }
        return selected.shuffled()
    }

    static func validate() -> [String] {
        var issues: [String] = []
        let ids = [writingQuestion.id] + all.map(\.id)
        if Set(ids).count != ids.count { issues.append("Question IDs must be unique.") }

        for question in all {
            if question.prompt.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                issues.append("\(question.id) has an empty prompt.")
            }
            if question.format == .essay {
                continue
            } else if question.format == .numericEntry {
                if question.acceptedNumericAnswers.isEmpty { issues.append("\(question.id) has no numeric answer.") }
            } else {
                let optionIDs = Set(question.groups.flatMap { $0.options.map(\.id) })
                let correctIDs = Set(question.correctSelections.values.flatMap { $0 })
                if !correctIDs.isSubset(of: optionIDs) { issues.append("\(question.id) has an invalid choice key.") }
                if correctIDs.isEmpty { issues.append("\(question.id) has no correct choice.") }
            }
            if let figure = question.figure {
                if figure.title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    issues.append("\(question.id) has an untitled figure.")
                }
                if Set(figure.series.map(\.name)).count != figure.series.count {
                    issues.append("\(question.id) has duplicate figure-series names.")
                }
                switch figure.kind {
                case .table:
                    if figure.headers?.isEmpty != false || figure.rows?.isEmpty != false {
                        issues.append("\(question.id) has an empty table.")
                    }
                case .venn:
                    if figure.annotations?.isEmpty != false {
                        issues.append("\(question.id) has an empty Venn diagram.")
                    }
                case .boxPlot:
                    let hasIncompleteSummary = figure.series.flatMap(\.points).contains { point in
                        point.low == nil || point.q1 == nil || point.median == nil || point.q3 == nil || point.high == nil
                    }
                    if figure.series.isEmpty || hasIncompleteSummary {
                        issues.append("\(question.id) has an incomplete box plot.")
                    }
                default:
                    if figure.series.isEmpty || figure.series.contains(where: { $0.points.isEmpty }) {
                        issues.append("\(question.id) has an empty chart series.")
                    }
                }
            }
        }

        let contentSignatures = all.map { question in
            [question.figure?.title, question.stimulus, question.prompt, question.quantityA, question.quantityB]
                .compactMap { $0 }
                .joined(separator: "|")
                .lowercased()
                .filter { !$0.isWhitespace && !$0.isPunctuation }
        }
        if Set(contentSignatures).count != contentSignatures.count {
            issues.append("Question content must be unique after normalization.")
        }

        for measure in [GREMeasure.verbal, .quantitative] {
            let count = all.filter { $0.measure == measure }.count
            if count < 27 { issues.append("\(measure.title) needs at least 27 questions; found \(count).") }
            for difficulty in QuestionDifficulty.allCases {
                let levelCount = all.filter { $0.measure == measure && $0.difficulty == difficulty }.count
                if levelCount < 15 { issues.append("\(measure.title) \(difficulty.title) pool needs 15 questions; found \(levelCount).") }
            }
        }
        return issues
    }

    private static func distance(_ lhs: QuestionDifficulty, from rhs: QuestionDifficulty) -> Int {
        let order: [QuestionDifficulty: Int] = [.easy: 0, .medium: 1, .hard: 2]
        return abs((order[lhs] ?? 0) - (order[rhs] ?? 0))
    }

    static func single(
        _ id: String,
        _ measure: GREMeasure,
        _ difficulty: QuestionDifficulty,
        stimulus: String? = nil,
        prompt: String,
        choices: [String],
        correct: Int,
        explanation: String,
        area: String
    ) -> GREQuestion {
        let options = choices.enumerated().map { ChoiceOption(id: "o\($0.offset)", text: $0.element) }
        return GREQuestion(
            id: id, measure: measure, difficulty: difficulty, format: .singleChoice,
            stimulus: stimulus, prompt: prompt, quantityA: nil, quantityB: nil,
            groups: [ChoiceGroup(id: "main", title: nil, options: options, maximumSelections: 1)],
            correctSelections: ["main": ["o\(correct)"]], acceptedNumericAnswers: [],
            explanation: explanation, contentArea: area
        )
    }

    static func multiple(
        _ id: String,
        _ measure: GREMeasure,
        _ difficulty: QuestionDifficulty,
        stimulus: String? = nil,
        prompt: String,
        choices: [String],
        correct: Set<Int>,
        maximum: Int,
        explanation: String,
        area: String,
        format: QuestionFormat = .multipleChoice
    ) -> GREQuestion {
        let options = choices.enumerated().map { ChoiceOption(id: "o\($0.offset)", text: $0.element) }
        return GREQuestion(
            id: id, measure: measure, difficulty: difficulty, format: format,
            stimulus: stimulus, prompt: prompt, quantityA: nil, quantityB: nil,
            groups: [ChoiceGroup(id: "main", title: nil, options: options, maximumSelections: maximum)],
            correctSelections: ["main": Set(correct.map { "o\($0)" })], acceptedNumericAnswers: [],
            explanation: explanation, contentArea: area
        )
    }

    static func completion(
        _ id: String,
        _ difficulty: QuestionDifficulty,
        stimulus: String? = nil,
        prompt: String,
        columns: [[String]],
        correct: [Int],
        explanation: String
    ) -> GREQuestion {
        let groups = columns.enumerated().map { column, choices in
            ChoiceGroup(
                id: "blank\(column)",
                title: columns.count == 1 ? nil : "Blank (\(roman(column + 1)))",
                options: choices.enumerated().map { ChoiceOption(id: "b\(column)o\($0.offset)", text: $0.element) },
                maximumSelections: 1
            )
        }
        var answers: [String: Set<String>] = [:]
        for (column, answer) in correct.enumerated() {
            answers["blank\(column)"] = ["b\(column)o\(answer)"]
        }
        return GREQuestion(
            id: id, measure: .verbal, difficulty: difficulty, format: .textCompletion,
            stimulus: stimulus, prompt: prompt, quantityA: nil, quantityB: nil,
            groups: groups, correctSelections: answers, acceptedNumericAnswers: [],
            explanation: explanation, contentArea: "Text Completion"
        )
    }

    static func equivalence(
        _ id: String,
        _ difficulty: QuestionDifficulty,
        prompt: String,
        choices: [String],
        correct: Set<Int>,
        explanation: String
    ) -> GREQuestion {
        multiple(
            id, .verbal, difficulty, prompt: prompt, choices: choices, correct: correct,
            maximum: 2, explanation: explanation, area: "Sentence Equivalence", format: .sentenceEquivalence
        )
    }

    static func numeric(
        _ id: String,
        _ difficulty: QuestionDifficulty,
        stimulus: String? = nil,
        prompt: String,
        answers: Set<String>,
        explanation: String,
        area: String
    ) -> GREQuestion {
        GREQuestion(
            id: id, measure: .quantitative, difficulty: difficulty, format: .numericEntry,
            stimulus: stimulus, prompt: prompt, quantityA: nil, quantityB: nil, groups: [],
            correctSelections: [:], acceptedNumericAnswers: answers,
            explanation: explanation, contentArea: area
        )
    }

    static func comparison(
        _ id: String,
        _ difficulty: QuestionDifficulty,
        stimulus: String? = nil,
        quantityA: String,
        quantityB: String,
        correct: Int,
        explanation: String,
        area: String
    ) -> GREQuestion {
        let choices = [
            "Quantity A is greater.",
            "Quantity B is greater.",
            "The two quantities are equal.",
            "The relationship cannot be determined from the information given."
        ]
        let options = choices.enumerated().map { ChoiceOption(id: "o\($0.offset)", text: $0.element) }
        return GREQuestion(
            id: id, measure: .quantitative, difficulty: difficulty, format: .quantitativeComparison,
            stimulus: stimulus, prompt: "Compare Quantity A and Quantity B.",
            quantityA: quantityA, quantityB: quantityB,
            groups: [ChoiceGroup(id: "main", title: nil, options: options, maximumSelections: 1)],
            correctSelections: ["main": ["o\(correct)"]], acceptedNumericAnswers: [],
            explanation: explanation, contentArea: area
        )
    }

    private static func roman(_ value: Int) -> String {
        [1: "i", 2: "ii", 3: "iii"][value] ?? "\(value)"
    }
}
