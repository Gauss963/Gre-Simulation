import SwiftUI

struct ScoreReportView: View {
    @ObservedObject var session: ExamSession
    let result: ExamResult
    let done: () -> Void

    @State private var reviewMeasure: GREMeasure?

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("GRE").font(.system(size: 22, weight: .black, design: .serif))
                Text("SIMULATION · SCORE REPORT").font(.caption.bold()).tracking(1.2)
                Spacer()
                Button("Save & Close", action: done)
                    .buttonStyle(.borderedProminent)
                    .tint(.white)
                    .foregroundStyle(GRETheme.navy)
            }
            .padding(.horizontal, 20)
            .frame(height: 58)
            .foregroundStyle(.white)
            .background(GRETheme.navy)

            ScrollView {
                VStack(alignment: .leading, spacing: 22) {
                    header
                    scoreCards
                    sectionPerformance
                    answerReview
                    methodology
                }
                .frame(maxWidth: 980, alignment: .leading)
                .padding(30)
            }
            .background(GRETheme.canvas)
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text("Practice test complete")
                .font(.system(size: 32, weight: .bold, design: .rounded))
                .foregroundStyle(GRETheme.navy)
            Text("\(result.mode.title) · \(result.completedAt.formatted(date: .abbreviated, time: .shortened))")
                .foregroundStyle(.secondary)
            Label("Scores below are practice estimates, not official GRE scores.", systemImage: "info.circle.fill")
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(GRETheme.warning)
                .padding(.top, 3)
        }
    }

    private var scoreCards: some View {
        HStack(spacing: 14) {
            if result.verbalTotal > 0 {
                scoreCard("Verbal Reasoning", result.verbalScore, raw: "\(result.verbalCorrect) of \(result.verbalTotal) correct", range: "130–170")
            }
            if result.quantitativeTotal > 0 {
                scoreCard("Quantitative Reasoning", result.quantitativeScore, raw: "\(result.quantitativeCorrect) of \(result.quantitativeTotal) correct", range: "130–170")
            }
            if let writing = result.writingEstimate {
                scoreCard("Analytical Writing", nil, customScore: String(format: "%.1f", writing), raw: "local structure signal", range: "0–6")
            }
        }
    }

    private func scoreCard(_ title: String, _ score: Int?, customScore: String? = nil, raw: String, range: String) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack {
                Text(title).font(.subheadline.weight(.semibold))
                Spacer()
                Text(range).font(.caption).foregroundStyle(.secondary)
            }
            Text(customScore ?? score.map(String.init) ?? "NS")
                .font(.system(size: 42, weight: .bold, design: .rounded))
                .monospacedDigit()
                .foregroundStyle(GRETheme.blue)
            Text(raw).font(.caption).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .greCard()
    }

    private var sectionPerformance: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Section performance").font(.title2.bold())
            ForEach(Array(result.sections.enumerated()), id: \.offset) { _, section in
                VStack(spacing: 8) {
                    HStack {
                        Text("\(section.measure.title) · Section \(section.ordinal)")
                            .fontWeight(.semibold)
                        if let difficulty = section.difficulty {
                            Text("\(difficulty.title) route")
                                .font(.caption.bold())
                                .foregroundStyle(GRETheme.blue)
                                .padding(.horizontal, 8)
                                .padding(.vertical, 4)
                                .background(GRETheme.brightBlue.opacity(0.1), in: Capsule())
                        }
                        Spacer()
                        Text(section.total > 0 ? "\(section.correct) / \(section.total)" : "Response recorded")
                            .font(section.total > 0 ? .headline.monospacedDigit() : .caption.weight(.semibold))
                            .foregroundStyle(section.total > 0 ? .primary : GRETheme.teal)
                    }
                    if section.total > 0 {
                        ProgressView(value: Double(section.correct), total: Double(section.total))
                            .tint(GRETheme.teal)
                    }
                }
                .padding(14)
                .background(GRETheme.canvas.opacity(0.75), in: RoundedRectangle(cornerRadius: 8))
            }
        }
        .greCard()
    }

    private var answerReview: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Review answers").font(.title2.bold())
            Text("Explanations are available only after the timed session ends.")
                .font(.subheadline).foregroundStyle(.secondary)

            ForEach([GREMeasure.verbal, .quantitative].filter { measureQuestions($0).isEmpty == false }) { measure in
                DisclosureGroup {
                    VStack(spacing: 10) {
                        ForEach(measureQuestions(measure)) { question in
                            reviewRow(question)
                        }
                    }
                    .padding(.top, 10)
                } label: {
                    Text(measure.title).font(.headline)
                }
                .padding(14)
                .background(GRETheme.canvas.opacity(0.65), in: RoundedRectangle(cornerRadius: 8))
            }
        }
        .greCard()
    }

    private func reviewRow(_ question: GREQuestion) -> some View {
        let correct = session.isCorrect(question)
        return VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .top) {
                Image(systemName: correct ? "checkmark.circle.fill" : "xmark.circle.fill")
                    .foregroundStyle(correct ? GRETheme.teal : .red)
                Text(question.prompt).font(.subheadline.weight(.semibold)).lineLimit(3)
                Spacer()
                Text(question.contentArea).font(.caption2).foregroundStyle(.secondary)
            }
            Text(question.explanation)
                .font(.caption)
                .foregroundStyle(.secondary)
                .padding(.leading, 27)
            VStack(alignment: .leading, spacing: 3) {
                Text("Your answer: \(answerText(for: question))")
                Text("Correct answer: \(correctAnswerText(for: question))")
                    .fontWeight(.semibold)
            }
            .font(.caption)
            .foregroundStyle(correct ? GRETheme.teal : .primary)
            .padding(.leading, 27)
            if let source = question.source {
                Label {
                    Text("\(source.title) · \(source.detail)")
                } icon: {
                    Image(systemName: source.isAuthorizedSourceItem ? "book.closed.fill" : "wand.and.stars")
                }
                .font(.caption2)
                .foregroundStyle(GRETheme.blue)
                .padding(.leading, 27)
            }
        }
        .padding(11)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(.white, in: RoundedRectangle(cornerRadius: 6))
    }

    private func answerText(for question: GREQuestion) -> String {
        let answer = session.answers[question.id] ?? GREAnswer()
        if question.format == .numericEntry {
            return answer.numericEntry.isEmpty ? "Not answered" : answer.numericEntry
        }
        let values = question.groups.flatMap { group in
            let selected = answer.selections[group.id] ?? []
            return group.options.filter { selected.contains($0.id) }.map(\.text)
        }
        return values.isEmpty ? "Not answered" : values.joined(separator: " · ")
    }

    private func correctAnswerText(for question: GREQuestion) -> String {
        if question.format == .numericEntry {
            return question.acceptedNumericAnswers.sorted().joined(separator: " or ")
        }
        return question.groups.flatMap { group in
            let keyed = question.correctSelections[group.id] ?? []
            return group.options.filter { keyed.contains($0.id) }.map(\.text)
        }
        .joined(separator: " · ")
    }

    private var methodology: some View {
        VStack(alignment: .leading, spacing: 8) {
            Label("How this estimate works", systemImage: "function")
                .font(.headline)
                .foregroundStyle(GRETheme.navy)
            Text("Verbal and Quant scores map raw accuracy linearly onto 130–170. The real GRE converts raw scores through proprietary equating and accounts for the selected second-section difficulty, so an official score may differ. The adaptive route in this simulator uses first-section accuracy: below 40% selects Easy, 40–66% Medium, and 67% or above Hard.")
                .font(.subheadline)
                .foregroundStyle(.secondary)
            Text("The writing signal uses response length, organization, transition cues, and lexical variety. It cannot judge factual relevance, logical quality, or style as a trained reader can.")
                .font(.subheadline)
                .foregroundStyle(.secondary)
        }
        .greCard()
    }

    private func measureQuestions(_ measure: GREMeasure) -> [GREQuestion] {
        session.sections
            .filter { section in
                section.measure == measure
                    && session.result?.sections.contains(where: { summary in
                        summary.measure == measure && summary.ordinal == section.ordinal
                    }) == true
            }
            .flatMap(\.questions)
    }
}
