import SwiftUI
import Combine

struct ExamContainerView: View {
    @StateObject private var session: ExamSession
    let onComplete: (ExamResult) -> Void
    let onExit: () -> Void

    @State private var confirmExit = false
    @State private var confirmFinish = false
    @State private var showReview = false
    @State private var showHelp = false

    private let timer = Timer.publish(every: 0.5, on: .main, in: .common).autoconnect()

    init(mode: ExamMode, onComplete: @escaping (ExamResult) -> Void, onExit: @escaping () -> Void) {
        _session = StateObject(wrappedValue: ExamSession(mode: mode))
        self.onComplete = onComplete
        self.onExit = onExit
    }

    var body: some View {
        Group {
            switch session.phase {
            case .directions:
                SectionDirectionsView(session: session, exit: { confirmExit = true })
            case .testing:
                TestInterfaceView(
                    session: session,
                    exit: { confirmExit = true },
                    review: { showReview = true },
                    help: { showHelp = true },
                    finish: { confirmFinish = true }
                )
            case .completed:
                if let result = session.result {
                    ScoreReportView(session: session, result: result) { onComplete(result) }
                }
            }
        }
        .interactiveDismissDisabled()
        .onReceive(timer) { _ in session.tick() }
        .onAppear {
            #if DEBUG
            if ProcessInfo.processInfo.arguments.contains("-completeExam") {
                session.completeWithCorrectAnswersForTesting()
            } else if ProcessInfo.processInfo.arguments.contains("-beginSection"), session.phase == .directions {
                session.beginSection()
            }
            #endif
        }
        .confirmationDialog("Exit this test?", isPresented: $confirmExit, titleVisibility: .visible) {
            Button("Exit without saving", role: .destructive, action: onExit)
            Button("Continue test", role: .cancel) {}
        } message: {
            Text("Your current test progress will be discarded.")
        }
        .confirmationDialog("End this section?", isPresented: $confirmFinish, titleVisibility: .visible) {
            Button("End section") { session.finishSection() }
            Button("Keep working", role: .cancel) {}
        } message: {
            Text("You have answered \(session.answeredCount) of \(session.currentSection.questions.count) questions. You cannot return after ending the section.")
        }
        .sheet(isPresented: $showReview) {
            ReviewView(session: session, dismiss: { showReview = false }, finish: {
                showReview = false
                confirmFinish = true
            })
        }
        .sheet(isPresented: $session.isCalculatorPresented) {
            CalculatorView()
        }
        .sheet(isPresented: $showHelp) {
            HelpView(question: session.currentQuestion)
        }
    }
}

private struct SectionDirectionsView: View {
    @ObservedObject var session: ExamSession
    let exit: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            examHeader(title: "General Test", exit: exit)
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    Text(session.currentSection.displayTitle)
                        .font(.system(size: 28, weight: .bold))
                        .foregroundStyle(GRETheme.navy)
                    Divider()
                    Text("Directions")
                        .font(.title3.bold())
                    Text(directions)
                        .font(.body)
                        .lineSpacing(6)
                    if session.currentSection.measure != .analyticalWriting {
                        VStack(alignment: .leading, spacing: 10) {
                            Label("You may skip questions and return to them within this section.", systemImage: "arrow.uturn.backward.circle")
                            Label("Use Mark and Review to track questions you want to revisit.", systemImage: "bookmark")
                            Label("Answers left blank are counted as incorrect; there is no penalty for guessing.", systemImage: "checkmark.circle")
                        }
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                    }
                    HStack {
                        Label("\(session.currentSection.questions.count) \(session.currentSection.questions.count == 1 ? "task" : "questions")", systemImage: "list.number")
                        Spacer()
                        Label(formatDuration(session.currentSection.durationSeconds), systemImage: "clock")
                    }
                    .font(.headline)
                    .padding(16)
                    .background(GRETheme.canvas, in: RoundedRectangle(cornerRadius: 8))
                    Text("Timing begins when you select Begin Section.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                    HStack {
                        Spacer()
                        Button("Begin Section") { session.beginSection() }
                            .buttonStyle(PrimaryGREButtonStyle())
                    }
                }
                .frame(maxWidth: 780, alignment: .leading)
                .greCard()
                .padding(30)
            }
            .background(GRETheme.canvas)
        }
    }

    private var directions: String {
        switch session.currentSection.measure {
        case .analyticalWriting:
            "Present your perspective on the issue below. Support your position with relevant reasons and examples. You may use Backspace, Delete, Cut, Paste, and Undo while composing. This simulator does not provide grammar or content feedback during the timed section."
        case .verbal:
            "This measure evaluates your ability to analyze written material, understand relationships among words and concepts, and reason from the information provided. Read each question's specific instruction carefully; some questions require more than one answer."
        case .quantitative:
            "This measure evaluates basic mathematical skills and quantitative reasoning in arithmetic, algebra, geometry, and data analysis. All numbers are real unless otherwise stated. Figures are not necessarily drawn to scale. An on-screen calculator is available."
        }
    }
}

private struct TestInterfaceView: View {
    @ObservedObject var session: ExamSession
    let exit: () -> Void
    let review: () -> Void
    let help: () -> Void
    let finish: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            examHeader(title: session.currentSection.displayTitle, exit: exit)
            testToolbar
            ProgressView(value: session.progress)
                .tint(GRETheme.brightBlue)
                .scaleEffect(x: 1, y: 0.55)
            ScrollView {
                QuestionView(session: session)
                    .frame(maxWidth: 1500, alignment: .leading)
                    .frame(maxWidth: .infinity, alignment: .center)
                    .padding(.horizontal, 32)
                    .padding(.vertical, 24)
            }
            .background(GRETheme.canvas)
            navigationFooter
        }
    }

    private var testToolbar: some View {
        HStack(spacing: 8) {
            Text("Question \(session.currentQuestionIndex + 1) of \(session.currentSection.questions.count)")
                .font(.subheadline.weight(.semibold))
                .monospacedDigit()
            Spacer()
            if session.currentSection.measure == .quantitative {
                toolbarButton("Calculator", "plus.forwardslash.minus") { session.isCalculatorPresented = true }
            }
            toolbarButton(session.isTimeVisible ? timeText : "Show Time", "clock") {
                session.isTimeVisible.toggle()
            }
            toolbarButton(session.isCurrentMarked ? "Marked" : "Mark", session.isCurrentMarked ? "bookmark.fill" : "bookmark") {
                session.toggleMark()
            }
            toolbarButton("Review", "square.grid.3x3") { review() }
            toolbarButton("Help", "questionmark.circle") { help() }
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 9)
        .background(GRETheme.surface)
        .overlay(alignment: .bottom) { Divider() }
    }

    private func toolbarButton(_ title: String, _ icon: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Label(title, systemImage: icon)
                .font(.caption.weight(.semibold))
                .lineLimit(1)
        }
        .buttonStyle(.bordered)
        .tint(GRETheme.blue)
    }

    private var navigationFooter: some View {
        HStack {
            Button("Back") { session.goBack() }
                .buttonStyle(SecondaryGREButtonStyle())
                .disabled(!session.canGoBack)
            Spacer()
            Text(session.currentAnswer.isEmpty ? "Not answered" : "Answer recorded")
                .font(.caption)
                .foregroundStyle(session.currentAnswer.isEmpty ? .secondary : GRETheme.teal)
            Spacer()
            if session.isLastQuestion {
                Button("Review Section") { review() }
                    .buttonStyle(PrimaryGREButtonStyle())
            } else {
                Button("Next") { session.goNext() }
                    .buttonStyle(PrimaryGREButtonStyle())
            }
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 12)
        .background(GRETheme.surface)
        .overlay(alignment: .top) { Divider() }
    }

    private var timeText: String {
        let minutes = session.secondsRemaining / 60
        let seconds = session.secondsRemaining % 60
        return String(format: "%02d:%02d", minutes, seconds)
    }
}

private struct QuestionView: View {
    @ObservedObject var session: ExamSession

    var body: some View {
        VStack(alignment: .leading, spacing: 19) {
            Text(session.currentQuestion.format.directions)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(GRETheme.navy)
                .padding(.bottom, 2)
            Divider()

            if session.currentQuestion.format == .essay {
                essayView
            } else {
                ViewThatFits(in: .horizontal) {
                    if hasSupportingMaterial {
                        HStack(alignment: .top, spacing: 28) {
                            supportingPanel.frame(minWidth: 410, maxWidth: .infinity)
                            responsePanel.frame(minWidth: 370, maxWidth: .infinity)
                        }
                    }
                    VStack(alignment: .leading, spacing: 20) {
                        if hasSupportingMaterial { supportingPanel }
                        responsePanel
                    }
                }
            }
        }
        .greCard()
    }

    private var hasSupportingMaterial: Bool {
        session.currentQuestion.stimulus != nil || session.currentQuestion.figure != nil
    }

    private var supportingPanel: some View {
        VStack(alignment: .leading, spacing: 14) {
            if let stimulus = session.currentQuestion.stimulus {
                Text(stimulus)
                    .font(.system(size: 17, design: .serif))
                    .lineSpacing(7)
            }
            if let figure = session.currentQuestion.figure {
                QuantFigureView(figure: figure)
            }
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(GRETheme.canvas, in: RoundedRectangle(cornerRadius: 6))
        .overlay(RoundedRectangle(cornerRadius: 6).stroke(GRETheme.border))
    }

    private var responsePanel: some View {
        VStack(alignment: .leading, spacing: 19) {
            Text(session.currentQuestion.prompt)
                .font(.system(size: 18, weight: .medium, design: .serif))
                .lineSpacing(6)

            if session.currentQuestion.format == .quantitativeComparison {
                comparisonView
            }

            if session.currentQuestion.format == .numericEntry {
                numericEntry
            } else {
                choiceGroups
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var comparisonView: some View {
        HStack(spacing: 0) {
            quantityColumn("Quantity A", session.currentQuestion.quantityA ?? "")
            Divider()
            quantityColumn("Quantity B", session.currentQuestion.quantityB ?? "")
        }
        .frame(minHeight: 105)
        .overlay(RoundedRectangle(cornerRadius: 4).stroke(GRETheme.border))
    }

    private func quantityColumn(_ title: String, _ value: String) -> some View {
        VStack(spacing: 11) {
            Text(title).font(.caption.bold()).foregroundStyle(.secondary)
            Text(value).font(.title3.weight(.semibold)).multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(14)
    }

    private var choiceGroups: some View {
        Group {
            if session.currentQuestion.groups.count > 1 {
                ViewThatFits(in: .horizontal) {
                HStack(alignment: .top, spacing: 12) { groupViews }
                    VStack(alignment: .leading, spacing: 14) { groupViews }
                }
            } else {
                VStack(alignment: .leading, spacing: 14) { groupViews }
            }
        }
    }

    private var groupViews: some View {
        ForEach(session.currentQuestion.groups) { group in
            VStack(alignment: .leading, spacing: 8) {
                if let title = group.title {
                    Text(title).font(.caption.bold()).foregroundStyle(.secondary)
                }
                ForEach(group.options) { option in
                    choiceButton(group: group, option: option)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private func choiceButton(group: ChoiceGroup, option: ChoiceOption) -> some View {
        let selected = session.currentAnswer.selections[group.id]?.contains(option.id) == true
        return Button {
            session.toggleSelection(groupID: group.id, optionID: option.id, maximum: group.maximumSelections)
        } label: {
            HStack(alignment: .top, spacing: 11) {
                Image(systemName: group.maximumSelections == 1
                      ? (selected ? "circle.inset.filled" : "circle")
                      : (selected ? "checkmark.square.fill" : "square"))
                    .foregroundStyle(selected ? GRETheme.blue : .secondary)
                    .font(.system(size: 18))
                Text(option.text)
                    .font(.system(size: 16))
                    .foregroundStyle(.primary)
                    .multilineTextAlignment(.leading)
                Spacer(minLength: 0)
            }
            .padding(11)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(selected ? GRETheme.brightBlue.opacity(0.16) : GRETheme.input)
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(selected ? GRETheme.brightBlue : GRETheme.border, lineWidth: selected ? 2 : 1))
            .clipShape(RoundedRectangle(cornerRadius: 5))
        }
        .buttonStyle(.plain)
    }

    private var numericEntry: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text("Numeric answer")
                .font(.caption.bold())
                .foregroundStyle(.secondary)
            TextField("Enter value", text: Binding(
                get: { session.currentAnswer.numericEntry },
                set: { session.updateNumericEntry($0) }
            ))
            .textFieldStyle(.plain)
            .font(.title3.monospacedDigit())
            .padding(12)
            .frame(maxWidth: 260)
            .background(GRETheme.input)
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(GRETheme.navy, lineWidth: 1.5))
        }
    }

    private var essayView: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(session.currentQuestion.prompt)
                .font(.system(size: 19, weight: .semibold, design: .serif))
                .lineSpacing(6)
            Text("Write a response in which you discuss the extent to which you agree or disagree with the statement. In developing your position, address the most compelling reasons or examples that could be used to challenge it.")
                .font(.system(size: 16, design: .serif))
                .lineSpacing(5)
                .foregroundStyle(.secondary)
            HStack {
                Text("Response")
                    .font(.caption.bold())
                    .foregroundStyle(.secondary)
                Spacer()
                Text("\(wordCount) words")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
            }
            TextEditor(text: Binding(
                get: { session.currentAnswer.essayText },
                set: { session.updateEssay($0) }
            ))
            .font(.system(size: 16, design: .serif))
            .autocorrectionDisabled()
            .padding(8)
            .frame(minHeight: 360)
            .scrollContentBackground(.hidden)
            .background(GRETheme.input)
            .overlay(RoundedRectangle(cornerRadius: 4).stroke(GRETheme.navy, lineWidth: 1.5))
        }
    }

    private var wordCount: Int {
        session.currentAnswer.essayText.split { $0.isWhitespace || $0.isPunctuation }.count
    }
}

private struct ReviewView: View {
    @ObservedObject var session: ExamSession
    let dismiss: () -> Void
    let finish: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Review Section").font(.title2.bold())
                    Text("Answered \(session.answeredCount) of \(session.currentSection.questions.count) · Marked \(session.markedCount)")
                        .font(.subheadline).foregroundStyle(.secondary)
                }
                Spacer()
                Button("Return") { dismiss() }.buttonStyle(.bordered)
            }
            .padding(20)
            Divider()

            ScrollView {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 130), spacing: 12)], spacing: 12) {
                    ForEach(Array(session.currentSection.questions.enumerated()), id: \.element.id) { index, question in
                        Button {
                            session.goToQuestion(index)
                            dismiss()
                        } label: {
                            HStack {
                                Text("Question \(index + 1)").fontWeight(.semibold)
                                Spacer()
                                statusIcon(session.status(for: question))
                            }
                            .padding(13)
                            .background(index == session.currentQuestionIndex ? GRETheme.brightBlue.opacity(0.12) : GRETheme.canvas)
                            .overlay(RoundedRectangle(cornerRadius: 7).stroke(index == session.currentQuestionIndex ? GRETheme.blue : GRETheme.border))
                            .clipShape(RoundedRectangle(cornerRadius: 7))
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(20)
            }
            Divider()
            HStack {
                Label("Answered", systemImage: "checkmark.circle.fill").foregroundStyle(GRETheme.teal)
                Label("Marked", systemImage: "bookmark.fill").foregroundStyle(GRETheme.warning)
                Spacer()
                Button("End Section", action: finish)
                    .buttonStyle(PrimaryGREButtonStyle())
            }
            .font(.caption)
            .padding(18)
        }
        .frame(minWidth: 600, minHeight: 500)
        .background(GRETheme.surface)
    }

    @ViewBuilder
    private func statusIcon(_ status: QuestionStatus) -> some View {
        switch status {
        case .unanswered: Image(systemName: "circle").foregroundStyle(.secondary)
        case .answered: Image(systemName: "checkmark.circle.fill").foregroundStyle(GRETheme.teal)
        case .marked: Image(systemName: "bookmark.fill").foregroundStyle(GRETheme.warning)
        }
    }
}

private struct HelpView: View {
    let question: GREQuestion
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack {
                Text("Question Help").font(.title2.bold())
                Spacer()
                Button("Done") { dismiss() }
            }
            Divider()
            Text(question.contentArea).font(.headline).foregroundStyle(GRETheme.blue)
            Text(question.format.directions).font(.body).lineSpacing(5)
            if question.measure == .quantitative {
                Text("All numbers are real unless otherwise stated. Geometric figures are not necessarily drawn to scale; graphical data presentations and coordinate systems are drawn to scale.")
                    .font(.subheadline).foregroundStyle(.secondary)
            }
            Text("Help explains the interface and directions only; it does not reveal the answer during a timed section.")
                .font(.footnote).foregroundStyle(.secondary)
            Spacer()
        }
        .padding(24)
        .frame(minWidth: 460, minHeight: 310)
    }
}

private func examHeader(title: String, exit: @escaping () -> Void) -> some View {
    HStack(spacing: 14) {
        Text("GRE")
            .font(.system(size: 22, weight: .black, design: .serif))
        Text("SIMULATION")
            .font(.caption.weight(.bold))
            .tracking(1.5)
        Rectangle().fill(.white.opacity(0.35)).frame(width: 1, height: 24)
        Text(title).font(.headline)
        Spacer()
        Button("Exit Test", action: exit)
            .font(.caption.weight(.semibold))
            .buttonStyle(.bordered)
            .tint(.white)
    }
    .foregroundStyle(.white)
    .padding(.horizontal, 18)
    .frame(height: 54)
    .background(GRETheme.headerNavy)
}

private func formatDuration(_ seconds: Int) -> String {
    let minutes = seconds / 60
    return "\(minutes) minutes"
}
