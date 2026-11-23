import SwiftUI

/// One Verbal question answered on its own: choices can be changed until it is checked, then the key,
/// your wrong choices, the explanation, and the guessing tip are shown.
struct VerbalDrillQuestionView: View {
    let question: GREQuestion
    @Binding var answer: GREAnswer
    /// nil until checked.
    let isCorrect: Bool?

    private var checked: Bool { isCorrect != nil }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(alignment: .firstTextBaseline) {
                if let type = VerbalDrillType.of(question) {
                    Text(type.title).font(.headline).foregroundStyle(GRETheme.navy)
                }
                Text(question.difficulty.rawValue.capitalized)
                    .font(.caption.bold())
                    .padding(.horizontal, 8).padding(.vertical, 3)
                    .background(GRETheme.brightBlue.opacity(0.14), in: Capsule())
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
                if hasPassage {
                    HStack(alignment: .top, spacing: 26) {
                        passage.frame(minWidth: 400, maxWidth: .infinity)
                        response.frame(minWidth: 360, maxWidth: .infinity)
                    }
                }
                VStack(alignment: .leading, spacing: 18) {
                    if hasPassage { passage }
                    response
                }
            }
            if checked { feedback }
        }
        .greCard()
    }

    private var hasPassage: Bool { question.stimulus != nil || question.format == .selectInPassage }

    private var passage: some View {
        VStack(alignment: .leading, spacing: 6) {
            if question.format == .selectInPassage, let group = question.groups.first {
                ForEach(group.options) { sentence in
                    let selected = answer.selections[group.id]?.contains(sentence.id) == true
                    let isKey = question.correctSelections[group.id]?.contains(sentence.id) == true
                    Text(sentence.text)
                        .font(.system(size: 17, design: .serif))
                        .lineSpacing(6)
                        .padding(.horizontal, 4).padding(.vertical, 2)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(fill(selected: selected, isKey: isKey), in: RoundedRectangle(cornerRadius: 3))
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
            if question.format != .selectInPassage {
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
            .background(fill(selected: selected, isKey: isKey))
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(stroke(selected: selected, isKey: isKey), lineWidth: selected || (checked && isKey) ? 2 : 1))
            .clipShape(RoundedRectangle(cornerRadius: 5))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    private func fill(selected: Bool, isKey: Bool) -> Color {
        guard checked else { return selected ? GRETheme.brightBlue.opacity(question.format == .selectInPassage ? 0.28 : 0.16) : (question.format == .selectInPassage ? .clear : GRETheme.input) }
        if isKey { return GRETheme.teal.opacity(0.2) }
        if selected { return Color.red.opacity(0.14) }
        return question.format == .selectInPassage ? .clear : GRETheme.input
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

    private var feedback: some View {
        VStack(alignment: .leading, spacing: 10) {
            VStack(alignment: .leading, spacing: 3) {
                Text("Your answer: \(VerbalDrill.answerText(answer.selections, for: question))")
                Text("Correct answer: \(VerbalDrill.answerText(question.correctSelections, for: question))")
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

            if let tip = QuestionBank.guessingTips[question.id] {
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

            if isCorrect == false, let type = VerbalDrillType.of(question) {
                Label(type.tip, systemImage: "target")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            if let source = question.source {
                Text("\(source.title) · \(source.detail)").font(.caption2).foregroundStyle(.tertiary)
            }
        }
    }
}

/// Verbal practice one question at a time, with immediate feedback, accuracy by question type, and an
/// answer history.
struct VerbalDrillView: View {
    enum TypeFilter: Hashable, Identifiable {
        case all
        case textCompletion
        case sentenceEquivalence
        case reading
        case single(VerbalDrillType)

        var id: String {
            switch self {
            case .all: "all"
            case .textCompletion: "tc"
            case .sentenceEquivalence: "se"
            case .reading: "rc"
            case .single(let type): type.rawValue
            }
        }

        var title: String {
            switch self {
            case .all: "All question types"
            case .textCompletion: "Text Completion (all)"
            case .sentenceEquivalence: "Sentence Equivalence"
            case .reading: "Reading Comprehension (all)"
            case .single(let type): type.title
            }
        }

        var types: Set<VerbalDrillType>? {
            switch self {
            case .all: nil
            case .textCompletion: [.textCompletion1, .textCompletion2, .textCompletion3]
            case .sentenceEquivalence: [.sentenceEquivalence]
            case .reading: [.readingSingle, .readingMultiple, .readingSelectSentence]
            case .single(let type): [type]
            }
        }

        static let menu: [TypeFilter] = [.all, .textCompletion, .single(.textCompletion1), .single(.textCompletion2), .single(.textCompletion3),
                                         .sentenceEquivalence, .reading, .single(.readingSingle), .single(.readingMultiple), .single(.readingSelectSentence)]
    }

    @StateObject private var store = VerbalDrillStore()
    @State private var typeFilter: TypeFilter = .all
    @State private var difficulty: QuestionDifficulty?
    @State private var focusWeak = false
    @State private var missedOnly = false
    @State private var current: GREQuestion?
    @State private var answer = GREAnswer()
    @State private var isCorrect: Bool?
    @State private var reviewing: VerbalDrillStore.Record?
    @State private var showsAllHistory = false
    @State private var sessionCorrect = 0
    @State private var sessionTotal = 0

    var body: some View {
        ScrollViewReader { proxy in
            ScrollView {
                content
                    .id("top")
            }
            .onChange(of: current?.id) { _, _ in proxy.scrollTo("top", anchor: .top) }
        }
        .navigationTitle("Verbal Practice")
        .onAppear { if current == nil { load(nextQuestion(after: nil)) } }
        .onChange(of: typeFilter) { _, _ in load(nextQuestion(after: nil)) }
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
                VerbalDrillQuestionView(question: current, answer: $answer, isCorrect: isCorrect)
                actions(current)
            } else {
                ContentUnavailableView {
                    Label(missedOnly ? "No missed questions" : "No matching questions", systemImage: "checkmark.seal")
                } description: {
                    Text(missedOnly ? "Every question you missed has since been answered correctly. Turn off Missed only to keep practicing." : "Choose a different question type or difficulty.")
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
            VStack(alignment: .leading, spacing: 6) {
                Text("Verbal practice")
                    .font(.system(size: 30, weight: .bold))
                    .foregroundStyle(GRETheme.navy)
                Text("Answer one question at a time, then check it to see the correct answer, the explanation, and the guessing strategy. Text Completion, Sentence Equivalence, and every Reading Comprehension format are included; reading questions on the same passage come one after another.")
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            ViewThatFits(in: .horizontal) {
                HStack(spacing: 16) { filters }
                VStack(alignment: .leading, spacing: 10) { filters }
            }
            HStack(spacing: 26) {
                stat("Questions practiced", "\(store.practicedCount) / \(VerbalDrill.pool.count)")
                stat("Accuracy (latest tries)", store.recentAccuracy.map { "\(Int(($0 * 100).rounded()))%" } ?? "—")
                stat("This session", sessionTotal == 0 ? "—" : "\(sessionCorrect) / \(sessionTotal)")
                stat("To redo", "\(store.missedCount)")
            }
            typeAccuracy
        }
        .greCard()
    }

    @ViewBuilder
    private var filters: some View {
        Picker("Type", selection: $typeFilter) {
            ForEach(TypeFilter.menu) { filter in
                Text(filter.title).tag(filter)
            }
        }
        .frame(maxWidth: 320)
        Picker("Difficulty", selection: $difficulty) {
            Text("All").tag(QuestionDifficulty?.none)
            Text("Easy").tag(QuestionDifficulty?.some(.easy))
            Text("Medium").tag(QuestionDifficulty?.some(.medium))
            Text("Hard").tag(QuestionDifficulty?.some(.hard))
        }
        .pickerStyle(.segmented)
        .frame(width: 290)
        Toggle("Focus on weak types", isOn: $focusWeak)
            .disabled(store.weakTypes.isEmpty)
            .help(store.weakTypes.isEmpty ? "Available after five answers of a type." : "Unpracticed questions of your two weakest types come first.")
        Toggle("Missed only", isOn: $missedOnly)
            .help("Only questions whose latest attempt was wrong.")
    }

    @ViewBuilder
    private var typeAccuracy: some View {
        let entries = store.accuracyByType.sorted { $0.accuracy < $1.accuracy }
        if !entries.isEmpty {
            let weak = Set(store.weakTypes)
            VStack(alignment: .leading, spacing: 6) {
                Text("Accuracy by question type (latest attempt of each question)").font(.caption).foregroundStyle(.secondary)
                ChipFlowLayout(spacing: 8, lineSpacing: 8) {
                    ForEach(entries) { entry in
                        HStack(spacing: 6) {
                            Text(entry.type.shortTitle).font(.caption.weight(.semibold))
                            Text("\(Int((entry.accuracy * 100).rounded()))%")
                                .font(.caption.bold().monospacedDigit())
                                .foregroundStyle(entry.accuracy >= 0.8 ? GRETheme.teal : (entry.accuracy >= 0.6 ? GRETheme.warning : .red))
                            Text("· \(entry.total)").font(.caption2).foregroundStyle(.secondary)
                        }
                        .padding(.horizontal, 10).padding(.vertical, 5)
                        .background(weak.contains(entry.type) ? Color.red.opacity(0.08) : GRETheme.input, in: Capsule())
                        .overlay(Capsule().stroke(weak.contains(entry.type) ? Color.red.opacity(0.35) : Color.clear))
                    }
                }
                if let first = store.weakTypes.first {
                    Label("Weakest: \(first.shortTitle) — \(first.tip)", systemImage: "lightbulb")
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
                Text(VerbalDrill.isComplete(answer, for: question) ? "Ready to check · ⌘↩" : completionHint(question))
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Spacer()
                Button("Skip") { load(nextQuestion(after: question)) }
                    .buttonStyle(SecondaryGREButtonStyle())
                Button("Check Answer") { check(question) }
                    .buttonStyle(PrimaryGREButtonStyle())
                    .keyboardShortcut(.return, modifiers: .command)
                    .disabled(!VerbalDrill.isComplete(answer, for: question))
            }
        }
        .greCard()
    }

    private func completionHint(_ question: GREQuestion) -> String {
        switch question.format {
        case .sentenceEquivalence: "Select exactly two answer choices."
        case .textCompletion where question.groups.count > 1: "Select one entry for each blank."
        case .multipleChoice: "Select every answer choice that applies."
        case .selectInPassage: "Click the sentence in the passage."
        default: "Select one answer choice."
        }
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
                                Text("\(record.question.flatMap(VerbalDrillType.of)?.title ?? "") · \(record.question?.difficulty.rawValue.capitalized ?? "") · \(record.date.formatted(date: .abbreviated, time: .shortened))")
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

    private func reviewSheet(_ record: VerbalDrillStore.Record) -> some View {
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
                    VerbalDrillQuestionView(question: question, answer: .constant(record.answer), isCorrect: record.isCorrect)
                        .padding(24)
                }
            }
        }
        .frame(minWidth: 760, idealWidth: 1000, minHeight: 600, idealHeight: 820)
        .background(GRETheme.canvas)
    }

    // MARK: - Actions

    private func nextQuestion(after current: GREQuestion?) -> GREQuestion? {
        let focus = focusWeak && !store.weakTypes.isEmpty ? Set(store.weakTypes) : nil
        return store.next(after: current, types: typeFilter.types, difficulty: difficulty, focus: focus, missedOnly: missedOnly)
    }

    private func load(_ question: GREQuestion?) {
        current = question
        answer = GREAnswer()
        isCorrect = nil
    }

    private func check(_ question: GREQuestion) {
        guard isCorrect == nil, VerbalDrill.isComplete(answer, for: question) else { return }
        let correct = store.record(question, answer: answer)
        isCorrect = correct
        sessionTotal += 1
        if correct { sessionCorrect += 1 }
    }

    #if DEBUG
    /// Renders a checked question of each type, for `-renderVerbalDrill <directory>`.
    static func renderSnapshots(to directory: URL) {
        for type in VerbalDrillType.allCases {
            guard let question = VerbalDrill.pool.first(where: { VerbalDrillType.of($0) == type && ($0.stimulus?.count ?? 0) < 1_200 }) else { continue }
            var answer = GREAnswer()
            for group in question.groups {
                let key = question.correctSelections[group.id] ?? []
                // Answer the first blank wrong so both colors show.
                if group.id == question.groups.first?.id, let wrong = group.options.first(where: { !key.contains($0.id) }) {
                    answer.selections[group.id] = question.format == .sentenceEquivalence ? [key.sorted().first!, wrong.id] : [wrong.id]
                } else {
                    answer.selections[group.id] = key
                }
            }
            let view = VerbalDrillQuestionView(question: question, answer: .constant(answer), isCorrect: false)
                .padding(24)
                .frame(width: 1050)
                .background(GRETheme.canvas)
            let renderer = ImageRenderer(content: view)
            renderer.scale = 1
            #if os(macOS)
            if let tiff = renderer.nsImage?.tiffRepresentation, let png = NSBitmapImageRep(data: tiff)?.representation(using: .png, properties: [:]) {
                try? png.write(to: directory.appendingPathComponent("verbal-\(type.rawValue).png"))
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
