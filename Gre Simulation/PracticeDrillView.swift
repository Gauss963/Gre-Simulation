import SwiftUI

/// One question answered on its own: the answer can be changed until it is checked, then the key, your
/// wrong choices, the explanation, and (for Verbal) the guessing tip are shown.
struct DrillQuestionView: View {
    let question: GREQuestion
    @Binding var answer: GREAnswer
    /// nil until checked.
    let isCorrect: Bool?

    private var checked: Bool { isCorrect != nil }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(alignment: .firstTextBaseline) {
                if let type = DrillType.of(question) {
                    Text(type.title).font(.headline).foregroundStyle(GRETheme.navy)
                }
                Text(question.difficulty.rawValue.capitalized)
                    .font(.caption.bold())
                    .padding(.horizontal, 8).padding(.vertical, 3)
                    .background(GRETheme.brightBlue.opacity(0.14), in: Capsule())
                if question.measure == .quantitative {
                    Text(DrillCategory.quantArea(of: question)).font(.caption).foregroundStyle(.secondary)
                }
                Spacer()
                if let isCorrect {
                    Label(isCorrect ? "Correct" : "Incorrect", systemImage: isCorrect ? "checkmark.circle.fill" : "xmark.circle.fill")
                        .font(.headline)
                        .foregroundStyle(isCorrect ? GRETheme.teal : .red)
                }
            }
            Text(question.format.directions)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            Divider()
            ViewThatFits(in: .horizontal) {
                if hasSupportingMaterial {
                    HStack(alignment: .top, spacing: 26) {
                        supportingPanel.frame(minWidth: 400, maxWidth: .infinity)
                        response.frame(minWidth: 360, maxWidth: .infinity)
                    }
                }
                VStack(alignment: .leading, spacing: 18) {
                    if hasSupportingMaterial { supportingPanel }
                    response
                }
            }
            if checked { feedback }
        }
        .greCard()
    }

    private var hasSupportingMaterial: Bool {
        question.stimulus != nil || question.figure != nil || question.format == .selectInPassage
    }

    private var supportingPanel: some View {
        VStack(alignment: .leading, spacing: 10) {
            if question.format == .selectInPassage, let group = question.groups.first {
                ForEach(group.options) { sentence in
                    let selected = answer.selections[group.id]?.contains(sentence.id) == true
                    let isKey = question.correctSelections[group.id]?.contains(sentence.id) == true
                    Text(sentence.text)
                        .font(.system(size: 17, design: .serif))
                        .lineSpacing(6)
                        .padding(.horizontal, 4).padding(.vertical, 2)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(fill(selected: selected, isKey: isKey, sentence: true), in: RoundedRectangle(cornerRadius: 3))
                        .contentShape(Rectangle())
                        .onTapGesture { toggle(group: group, option: sentence) }
                        .accessibilityAddTraits(.isButton)
                }
                if !checked {
                    Text("Click a sentence to select it.").font(.caption).foregroundStyle(.secondary)
                }
            } else if let stimulus = question.stimulus {
                Text(stimulus)
                    .font(.system(size: 17, design: .serif))
                    .lineSpacing(7)
                    .textSelection(.enabled)
                    .fixedSize(horizontal: false, vertical: true)
            }
            if let figure = question.figure {
                QuantFigureView(figure: figure)
            }
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(GRETheme.canvas, in: RoundedRectangle(cornerRadius: 6))
        .overlay(RoundedRectangle(cornerRadius: 6).stroke(GRETheme.border))
    }

    private var response: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(question.prompt)
                .font(.system(size: 18, weight: .medium, design: .serif))
                .lineSpacing(6)
                .textSelection(.enabled)
                .fixedSize(horizontal: false, vertical: true)
            if question.format == .quantitativeComparison { comparison }
            if question.format == .numericEntry {
                numericEntry
            } else if question.format != .selectInPassage {
                if question.groups.count > 1 {
                    ViewThatFits(in: .horizontal) {
                        HStack(alignment: .top, spacing: 12) { groups }
                        VStack(alignment: .leading, spacing: 14) { groups }
                    }
                } else {
                    VStack(alignment: .leading, spacing: 14) { groups }
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var comparison: some View {
        HStack(spacing: 0) {
            quantity("Quantity A", question.quantityA ?? "")
            Divider()
            quantity("Quantity B", question.quantityB ?? "")
        }
        .frame(minHeight: 105)
        .overlay(RoundedRectangle(cornerRadius: 4).stroke(GRETheme.border))
    }

    private func quantity(_ title: String, _ value: String) -> some View {
        VStack(spacing: 11) {
            Text(title).font(.caption.bold()).foregroundStyle(.secondary)
            Text(value).font(.title3.weight(.semibold)).multilineTextAlignment(.center).textSelection(.enabled)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(14)
    }

    private var groups: some View {
        ForEach(question.groups) { group in
            VStack(alignment: .leading, spacing: 8) {
                if let title = group.title {
                    Text(title).font(.caption.bold()).foregroundStyle(.secondary)
                }
                ForEach(group.options) { option in
                    choice(group: group, option: option)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private func choice(group: ChoiceGroup, option: ChoiceOption) -> some View {
        let selected = answer.selections[group.id]?.contains(option.id) == true
        let isKey = question.correctSelections[group.id]?.contains(option.id) == true
        let single = group.maximumSelections == 1
        return Button {
            toggle(group: group, option: option)
        } label: {
            HStack(alignment: .top, spacing: 11) {
                Image(systemName: single ? (selected ? "circle.inset.filled" : "circle") : (selected ? "checkmark.square.fill" : "square"))
                    .foregroundStyle(selected ? GRETheme.blue : .secondary)
                    .font(.system(size: 18))
                Text(option.text)
                    .font(.system(size: 16))
                    .foregroundStyle(.primary)
                    .multilineTextAlignment(.leading)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 0)
                if checked, isKey {
                    Image(systemName: "checkmark").foregroundStyle(GRETheme.teal).font(.body.bold())
                } else if checked, selected {
                    Image(systemName: "xmark").foregroundStyle(.red).font(.body.bold())
                }
            }
            .padding(11)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(fill(selected: selected, isKey: isKey, sentence: false))
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(stroke(selected: selected, isKey: isKey), lineWidth: selected || (checked && isKey) ? 2 : 1))
            .clipShape(RoundedRectangle(cornerRadius: 5))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    // MARK: Numeric Entry

    private var isFraction: Bool { PracticeDrill.isFractionEntry(question) }

    private var numericEntry: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text(isFraction ? "Enter your answer as a fraction. Fractions do not need to be reduced to lowest terms." : "Enter your answer as an integer or a decimal.")
                .font(.caption.bold())
                .foregroundStyle(.secondary)
            if isFraction {
                VStack(spacing: 6) {
                    numericBox(fractionPart(0), width: 120)
                    Rectangle().fill(GRETheme.navy).frame(width: 120, height: 2)
                    numericBox(fractionPart(1), width: 120)
                }
                .frame(width: 140)
            } else {
                numericBox(Binding(
                    get: { answer.numericEntry },
                    set: { if !checked { answer.numericEntry = Self.cleanNumber($0, allowDecimal: true) } }
                ), width: 220)
            }
        }
    }

    @ViewBuilder
    private func numericBox(_ text: Binding<String>, width: CGFloat) -> some View {
        if checked {
            // Once checked the entry is shown as text, colored by the result.
            Text(text.wrappedValue.isEmpty ? " " : text.wrappedValue)
                .font(.title3.monospacedDigit())
                .padding(10)
                .frame(width: width)
                .background(isCorrect == true ? GRETheme.teal.opacity(0.2) : Color.red.opacity(0.14))
                .overlay(RoundedRectangle(cornerRadius: 3).stroke(isCorrect == true ? GRETheme.teal : .red, lineWidth: 1.5))
        } else {
            TextField("", text: text)
                .textFieldStyle(.plain)
                .font(.title3.monospacedDigit())
                .multilineTextAlignment(.center)
                .padding(10)
                .frame(width: width)
                .background(GRETheme.input)
                .overlay(RoundedRectangle(cornerRadius: 3).stroke(GRETheme.navy, lineWidth: 1.5))
        }
    }

    private func fractionPart(_ index: Int) -> Binding<String> {
        Binding(
            get: {
                let parts = answer.numericEntry.split(separator: "/", omittingEmptySubsequences: false).map(String.init)
                return parts.indices.contains(index) ? parts[index] : ""
            },
            set: { value in
                guard !checked else { return }
                var parts = answer.numericEntry.split(separator: "/", omittingEmptySubsequences: false).map(String.init)
                while parts.count < 2 { parts.append("") }
                parts[index] = Self.cleanNumber(value, allowDecimal: false)
                answer.numericEntry = parts.allSatisfy(\.isEmpty) ? "" : parts.joined(separator: "/")
            }
        )
    }

    /// Digits, a leading minus sign, and (outside fraction boxes) one decimal point.
    static func cleanNumber(_ value: String, allowDecimal: Bool) -> String {
        var result = ""
        for character in value {
            if character.isNumber { result.append(character) }
            else if character == "-" || character == "−", result.isEmpty { result.append("-") }
            else if allowDecimal, character == ".", !result.contains(".") { result.append(".") }
        }
        return String(result.prefix(10))
    }

    // MARK: Colors and selection

    private func fill(selected: Bool, isKey: Bool, sentence: Bool) -> Color {
        guard checked else { return selected ? GRETheme.brightBlue.opacity(sentence ? 0.28 : 0.16) : (sentence ? .clear : GRETheme.input) }
        if isKey { return GRETheme.teal.opacity(0.2) }
        if selected { return Color.red.opacity(0.14) }
        return sentence ? .clear : GRETheme.input
    }

    private func stroke(selected: Bool, isKey: Bool) -> Color {
        guard checked else { return selected ? GRETheme.brightBlue : GRETheme.border }
        if isKey { return GRETheme.teal }
        if selected { return .red }
        return GRETheme.border
    }

    private func toggle(group: ChoiceGroup, option: ChoiceOption) {
        guard !checked else { return }
        var selections = answer.selections[group.id] ?? []
        if selections.contains(option.id) {
            selections.remove(option.id)
        } else if group.maximumSelections == 1 {
            selections = [option.id]
        } else if selections.count < group.maximumSelections {
            selections.insert(option.id)
        }
        answer.selections[group.id] = selections
    }

    // MARK: Feedback

    private var feedback: some View {
        VStack(alignment: .leading, spacing: 10) {
            VStack(alignment: .leading, spacing: 3) {
                Text("Your answer: \(PracticeDrill.answerText(answer, for: question))")
                Text("Correct answer: \(PracticeDrill.correctAnswerText(for: question))")
                    .fontWeight(.semibold)
            }
            .font(.callout)
            .foregroundStyle(isCorrect == true ? GRETheme.teal : .primary)
            .fixedSize(horizontal: false, vertical: true)

            VStack(alignment: .leading, spacing: 4) {
                Text("詳解").font(.caption.bold()).foregroundStyle(.secondary)
                Text(question.explanation)
                    .font(.callout)
                    .textSelection(.enabled)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(10)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(GRETheme.input, in: RoundedRectangle(cornerRadius: 6))

            if question.measure == .verbal, let tip = QuestionBank.guessingTips[question.id] {
                HStack(alignment: .top, spacing: 6) {
                    Image(systemName: "lightbulb.fill").foregroundStyle(.orange)
                    VStack(alignment: .leading, spacing: 2) {
                        Text("猜題法：單字不熟也能縮小範圍").font(.caption.weight(.semibold))
                        Text(tip)
                            .font(.callout)
                            .foregroundStyle(.secondary)
                            .textSelection(.enabled)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                .padding(10)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(Color.orange.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
            }

            if isCorrect == false {
                if let type = DrillType.of(question) {
                    Label(type.tip, systemImage: "target")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                if question.measure == .quantitative, let area = DrillCategory.of(question) {
                    Label(area.tip, systemImage: "function")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            if let source = question.source {
                Text("\(source.title) · \(source.detail)").font(.caption2).foregroundStyle(.tertiary)
            }
        }
    }
}

/// Practice one question at a time for one measure, with immediate feedback, accuracy by question type
/// (Verbal) or content area (Quant), and an answer history.
struct PracticeDrillView: View {
    /// The type filter's entries: everything, a family of types, or one type.
    struct TypeFilter: Hashable, Identifiable {
        let id: String
        let title: String
        let types: Set<DrillType>?

        static func single(_ type: DrillType) -> TypeFilter { TypeFilter(id: type.rawValue, title: type.title, types: [type]) }

        static func menu(for measure: GREMeasure) -> [TypeFilter] {
            let all = TypeFilter(id: "all", title: "All question types", types: nil)
            if measure == .quantitative {
                return [all, .single(.quantitativeComparison), .single(.quantSingle), .single(.quantMultiple), .single(.numericEntry)]
            }
            return [
                all,
                TypeFilter(id: "tc", title: "Text Completion (all)", types: [.textCompletion1, .textCompletion2, .textCompletion3]),
                .single(.textCompletion1), .single(.textCompletion2), .single(.textCompletion3),
                TypeFilter(id: "se", title: "Sentence Equivalence", types: [.sentenceEquivalence]),
                TypeFilter(id: "rc", title: "Reading Comprehension (all)", types: [.readingSingle, .readingMultiple, .readingSelectSentence]),
                .single(.readingSingle), .single(.readingMultiple), .single(.readingSelectSentence),
            ]
        }
    }

    let measure: GREMeasure

    @StateObject private var store: PracticeDrillStore
    @StateObject private var calculator = CalculatorModel()
    @State private var typeFilter: TypeFilter
    @State private var area: String?
    @State private var difficulty: QuestionDifficulty?
    @State private var focusWeak = false
    @State private var missedOnly = false
    @State private var current: GREQuestion?
    @State private var answer = GREAnswer()
    @State private var isCorrect: Bool?
    @State private var reviewing: PracticeDrillStore.Record?
    @State private var showsAllHistory = false
    @State private var showsCalculator = false
    @State private var sessionCorrect = 0
    @State private var sessionTotal = 0

    init(measure: GREMeasure) {
        self.measure = measure
        _store = StateObject(wrappedValue: PracticeDrillStore(measure: measure))
        _typeFilter = State(initialValue: TypeFilter.menu(for: measure)[0])
    }

    private var isQuant: Bool { measure == .quantitative }

    var body: some View {
        ScrollViewReader { proxy in
            ScrollView {
                content.id("top")
            }
            .onChange(of: current?.id) { _, _ in proxy.scrollTo("top", anchor: .top) }
        }
        .overlay(alignment: .topTrailing) {
            if isQuant, showsCalculator { floatingCalculator }
        }
        .navigationTitle(isQuant ? "Quant Practice" : "Verbal Practice")
        .onAppear { if current == nil { load(nextQuestion(after: nil)) } }
        .onChange(of: typeFilter) { _, _ in load(nextQuestion(after: nil)) }
        .onChange(of: area) { _, _ in load(nextQuestion(after: nil)) }
        .onChange(of: difficulty) { _, _ in load(nextQuestion(after: nil)) }
        .onChange(of: focusWeak) { _, _ in load(nextQuestion(after: nil)) }
        .onChange(of: missedOnly) { _, _ in load(nextQuestion(after: nil)) }
        .sheet(item: $reviewing) { record in reviewSheet(record) }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 22) {
            header
            if let current {
                if let last = store.lastResult(for: current), isCorrect == nil {
                    Label("Last time \(last.isCorrect ? "correct" : "incorrect") · \(last.date.formatted(date: .abbreviated, time: .omitted))",
                          systemImage: last.isCorrect ? "checkmark.circle" : "arrow.uturn.backward.circle")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                DrillQuestionView(question: current, answer: $answer, isCorrect: isCorrect)
                actions(current)
            } else {
                ContentUnavailableView {
                    Label(missedOnly ? "No missed questions" : "No matching questions", systemImage: "checkmark.seal")
                } description: {
                    Text(missedOnly ? "Every question you missed has since been answered correctly. Turn off Missed only to keep practicing." : "Choose a different question type, content area, or difficulty.")
                }
                .frame(maxWidth: .infinity, minHeight: 260)
                .greCard()
            }
            if !store.history.isEmpty { historyList }
        }
        .frame(maxWidth: 1100, alignment: .leading)
        .padding(30)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 6) {
                    Text(isQuant ? "Quant practice" : "Verbal practice")
                        .font(.system(size: 30, weight: .bold))
                        .foregroundStyle(GRETheme.navy)
                    Text(isQuant
                         ? "Answer one question at a time, then check it to see the correct answer and the explanation. Quantitative Comparison, multiple choice (one or more answers), and Numeric Entry are all included, with the on-screen calculator as on the real test."
                         : "Answer one question at a time, then check it to see the correct answer, the explanation, and the guessing strategy. Text Completion, Sentence Equivalence, and every Reading Comprehension format are included; reading questions on the same passage come one after another.")
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer()
                if isQuant {
                    Button {
                        showsCalculator.toggle()
                    } label: {
                        Label(showsCalculator ? "Hide Calculator" : "Calculator", systemImage: "plusminus")
                    }
                    .buttonStyle(SecondaryGREButtonStyle())
                }
            }
            ViewThatFits(in: .horizontal) {
                HStack(spacing: 16) { filters }
                VStack(alignment: .leading, spacing: 10) { filters }
            }
            HStack(spacing: 26) {
                stat("Questions practiced", "\(store.practicedCount) / \(store.pool.count)")
                stat("Accuracy (latest tries)", store.recentAccuracy.map { "\(Int(($0 * 100).rounded()))%" } ?? "—")
                stat("This session", sessionTotal == 0 ? "—" : "\(sessionCorrect) / \(sessionTotal)")
                stat("To redo", "\(store.missedCount)")
            }
            categoryAccuracy
        }
        .greCard()
    }

    @ViewBuilder
    private var filters: some View {
        Picker("Type", selection: $typeFilter) {
            ForEach(TypeFilter.menu(for: measure)) { filter in
                Text(filter.title).tag(filter)
            }
        }
        .frame(maxWidth: 320)
        if isQuant {
            Picker("Area", selection: $area) {
                Text("All areas").tag(String?.none)
                ForEach(DrillCategory.quantAreas) { area in
                    Text(area.id).tag(String?.some(area.id))
                }
            }
            .frame(maxWidth: 220)
        }
        Picker("Difficulty", selection: $difficulty) {
            Text("All").tag(QuestionDifficulty?.none)
            Text("Easy").tag(QuestionDifficulty?.some(.easy))
            Text("Medium").tag(QuestionDifficulty?.some(.medium))
            Text("Hard").tag(QuestionDifficulty?.some(.hard))
        }
        .pickerStyle(.segmented)
        .frame(width: 290)
        Toggle(isQuant ? "Focus on weak areas" : "Focus on weak types", isOn: $focusWeak)
            .disabled(store.weakCategories.isEmpty)
            .help(store.weakCategories.isEmpty
                  ? "Available after five answers in a \(isQuant ? "content area" : "question type")."
                  : "Unpracticed questions from your two weakest \(isQuant ? "content areas" : "question types") come first.")
        Toggle("Missed only", isOn: $missedOnly)
            .help("Only questions whose latest attempt was wrong.")
    }

    @ViewBuilder
    private var categoryAccuracy: some View {
        let entries = store.accuracyByCategory.sorted { $0.accuracy < $1.accuracy }
        if !entries.isEmpty {
            let weak = Set(store.weakCategories.map(\.id))
            VStack(alignment: .leading, spacing: 6) {
                Text("Accuracy by \(isQuant ? "content area" : "question type") (latest attempt of each question)")
                    .font(.caption).foregroundStyle(.secondary)
                ChipFlowLayout(spacing: 8, lineSpacing: 8) {
                    ForEach(entries) { entry in
                        HStack(spacing: 6) {
                            Text(entry.category.title).font(.caption.weight(.semibold))
                            Text("\(Int((entry.accuracy * 100).rounded()))%")
                                .font(.caption.bold().monospacedDigit())
                                .foregroundStyle(entry.accuracy >= 0.8 ? GRETheme.teal : (entry.accuracy >= 0.6 ? GRETheme.warning : .red))
                            Text("· \(entry.total)").font(.caption2).foregroundStyle(.secondary)
                        }
                        .padding(.horizontal, 10).padding(.vertical, 5)
                        .background(weak.contains(entry.id) ? Color.red.opacity(0.08) : GRETheme.input, in: Capsule())
                        .overlay(Capsule().stroke(weak.contains(entry.id) ? Color.red.opacity(0.35) : Color.clear))
                    }
                }
                if let first = store.weakCategories.first {
                    Label("Weakest: \(first.title) — \(first.tip)", systemImage: "lightbulb")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
        }
    }

    private func stat(_ title: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            Text(value).font(.title2.bold()).monospacedDigit()
        }
    }

    private func actions(_ question: GREQuestion) -> some View {
        HStack(spacing: 12) {
            if let isCorrect {
                Label(isCorrect ? "Correct" : "Incorrect", systemImage: isCorrect ? "checkmark.circle.fill" : "xmark.circle.fill")
                    .font(.title3.bold())
                    .foregroundStyle(isCorrect ? GRETheme.teal : .red)
                Spacer()
                Button("Try Again") { load(question) }
                    .buttonStyle(SecondaryGREButtonStyle())
                Button("Next Question") { load(nextQuestion(after: question)) }
                    .buttonStyle(PrimaryGREButtonStyle())
                    .keyboardShortcut(.return, modifiers: .command)
            } else {
                Text(PracticeDrill.isComplete(answer, for: question) ? "Ready to check · ⌘↩" : PracticeDrill.completionHint(question))
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Spacer()
                Button("Skip") { load(nextQuestion(after: question)) }
                    .buttonStyle(SecondaryGREButtonStyle())
                Button("Check Answer") { check(question) }
                    .buttonStyle(PrimaryGREButtonStyle())
                    .keyboardShortcut(.return, modifiers: .command)
                    .disabled(!PracticeDrill.isComplete(answer, for: question))
            }
        }
        .greCard()
    }

    private var floatingCalculator: some View {
        let canTransfer = current.map { $0.format == .numericEntry && !PracticeDrill.isFractionEntry($0) } == true && isCorrect == nil
        return CalculatorView(
            model: calculator,
            canTransfer: canTransfer,
            transfer: { value in if canTransfer, !value.isEmpty { answer.numericEntry = DrillQuestionView.cleanNumber(value, allowDecimal: true) } },
            close: { showsCalculator = false }
        )
        .padding(.top, 90)
        .padding(.trailing, 30)
    }

    private var historyList: some View {
        let records = showsAllHistory ? store.history : Array(store.history.prefix(10))
        return VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Answer history").font(.headline)
                Spacer()
                if store.history.count > 10 {
                    Button(showsAllHistory ? "Show recent" : "Show all \(store.history.count)") { showsAllHistory.toggle() }
                        .buttonStyle(.borderless)
                }
            }
            LazyVStack(spacing: 8) {
                ForEach(records) { record in
                    Button { reviewing = record } label: {
                        HStack(spacing: 12) {
                            Image(systemName: record.isCorrect ? "checkmark.circle.fill" : "xmark.circle.fill")
                                .foregroundStyle(record.isCorrect ? GRETheme.teal : .red)
                            VStack(alignment: .leading, spacing: 2) {
                                Text(record.question?.prompt ?? "Question")
                                    .font(.subheadline.weight(.semibold))
                                    .lineLimit(1)
                                Text(historyDetail(record))
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            Spacer()
                            Image(systemName: "chevron.right").font(.caption).foregroundStyle(.secondary)
                        }
                        .padding(10)
                        .contentShape(Rectangle())
                        .background(GRETheme.input, in: RoundedRectangle(cornerRadius: 8))
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .greCard()
    }

    private func historyDetail(_ record: PracticeDrillStore.Record) -> String {
        guard let question = record.question else { return "" }
        var parts = [DrillType.of(question)?.title ?? ""]
        if isQuant { parts.append(DrillCategory.quantArea(of: question)) }
        parts.append(question.difficulty.rawValue.capitalized)
        parts.append(record.date.formatted(date: .abbreviated, time: .shortened))
        return parts.joined(separator: " · ")
    }

    private func reviewSheet(_ record: PracticeDrillStore.Record) -> some View {
        VStack(spacing: 0) {
            HStack {
                Text(record.date.formatted(date: .abbreviated, time: .shortened)).font(.headline)
                Spacer()
                if let question = record.question {
                    Button("Practice Again") { reviewing = nil; load(question) }
                        .buttonStyle(SecondaryGREButtonStyle())
                }
                Button("Done") { reviewing = nil }
                    .buttonStyle(PrimaryGREButtonStyle())
                    .keyboardShortcut(.cancelAction)
            }
            .padding(20)
            Divider()
            ScrollView {
                if let question = record.question {
                    DrillQuestionView(question: question, answer: .constant(record.answer), isCorrect: record.isCorrect)
                        .padding(24)
                }
            }
        }
        .frame(minWidth: 760, idealWidth: 1000, minHeight: 600, idealHeight: 820)
        .background(GRETheme.canvas)
    }

    // MARK: - Actions

    private func nextQuestion(after current: GREQuestion?) -> GREQuestion? {
        let focus = focusWeak && !store.weakCategories.isEmpty ? Set(store.weakCategories.map(\.id)) : nil
        return store.next(after: current, types: typeFilter.types, area: area, difficulty: difficulty, focus: focus, missedOnly: missedOnly)
    }

    private func load(_ question: GREQuestion?) {
        current = question
        answer = GREAnswer()
        isCorrect = nil
    }

    private func check(_ question: GREQuestion) {
        guard isCorrect == nil, PracticeDrill.isComplete(answer, for: question) else { return }
        let correct = store.record(question, answer: answer)
        isCorrect = correct
        sessionTotal += 1
        if correct { sessionCorrect += 1 }
    }

    #if DEBUG
    /// Renders a checked question of each type, for `-renderPracticeDrill <directory>`.
    static func renderSnapshots(to directory: URL) {
        for type in DrillType.allCases {
            let pool = PracticeDrill.pool(for: type.measure)
            guard let question = pool.first(where: { DrillType.of($0) == type && ($0.stimulus?.count ?? 0) < 1_200 && (type != .numericEntry || $0.figure != nil) })
                    ?? pool.first(where: { DrillType.of($0) == type }) else { continue }
            var answer = GREAnswer()
            if question.format == .numericEntry {
                answer.numericEntry = PracticeDrill.isFractionEntry(question) ? "1/3" : "12.5"
            }
            for group in question.groups {
                let key = question.correctSelections[group.id] ?? []
                // Answer the first group wrong so both colors show.
                if group.id == question.groups.first?.id, let wrong = group.options.first(where: { !key.contains($0.id) }) {
                    answer.selections[group.id] = question.format == .sentenceEquivalence ? [key.sorted().first!, wrong.id] : [wrong.id]
                } else {
                    answer.selections[group.id] = key
                }
            }
            let view = DrillQuestionView(question: question, answer: .constant(answer), isCorrect: false)
                .padding(24)
                .frame(width: 1050)
                .background(GRETheme.canvas)
            let renderer = ImageRenderer(content: view)
            renderer.scale = 1
            #if os(macOS)
            if let tiff = renderer.nsImage?.tiffRepresentation, let png = NSBitmapImageRep(data: tiff)?.representation(using: .png, properties: [:]) {
                try? png.write(to: directory.appendingPathComponent("drill-\(type.rawValue).png"))
            }
            #endif
        }
    }
    #endif
}

/// Wraps chips onto as many lines as needed.
struct ChipFlowLayout: Layout {
    var spacing: CGFloat = 8
    var lineSpacing: CGFloat = 8

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let width = proposal.width ?? .infinity
        var x: CGFloat = 0, y: CGFloat = 0, lineHeight: CGFloat = 0, widest: CGFloat = 0
        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if x > 0, x + size.width > width {
                y += lineHeight + lineSpacing
                x = 0
                lineHeight = 0
            }
            x += size.width + spacing
            widest = max(widest, x - spacing)
            lineHeight = max(lineHeight, size.height)
        }
        return CGSize(width: min(widest, width), height: y + lineHeight)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        var x = bounds.minX, y = bounds.minY, lineHeight: CGFloat = 0
        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if x > bounds.minX, x + size.width > bounds.maxX {
                y += lineHeight + lineSpacing
                x = bounds.minX
                lineHeight = 0
            }
            subview.place(at: CGPoint(x: x, y: y), proposal: ProposedViewSize(size))
            x += size.width + spacing
            lineHeight = max(lineHeight, size.height)
        }
    }
}
