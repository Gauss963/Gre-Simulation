import SwiftUI

struct DashboardView: View {
    let history: [ExamResult]
    let start: (ExamMode) -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 26) {
                hero
                if let latest = history.first { latestScore(latest) }
                testCards
                formatOverview
                accuracyNotice
            }
            .frame(maxWidth: 1050, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Practice Tests")
    }

    private var hero: some View {
        HStack(alignment: .center, spacing: 28) {
            VStack(alignment: .leading, spacing: 12) {
                Text("Practice the current GRE format")
                    .font(.system(size: 34, weight: .bold, design: .rounded))
                    .foregroundStyle(GRETheme.navy)
                Text("Timed sections, section-level adaptation, mark and review, an on-screen calculator, and transparent score estimates — designed for focused rehearsal on Mac and iPad.")
                    .font(.title3)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
                Label("\(QuestionBank.all.count) questions · \(VocabularyBank.words.count) vocabulary entries", systemImage: "books.vertical.fill")
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(GRETheme.teal)
                Button("Start full-length test") { start(.fullLength) }
                    .buttonStyle(PrimaryGREButtonStyle())
                    .padding(.top, 4)
            }
            Spacer(minLength: 12)
            ZStack {
                Circle().fill(GRETheme.brightBlue.opacity(0.10)).frame(width: 185, height: 185)
                Circle().stroke(GRETheme.brightBlue.opacity(0.20), lineWidth: 18).frame(width: 145, height: 145)
                VStack(spacing: 2) {
                    Text("1:58")
                        .font(.system(size: 36, weight: .bold, design: .monospaced))
                        .foregroundStyle(GRETheme.blue)
                    Text("5 sections")
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.secondary)
                }
            }
            .accessibilityHidden(true)
        }
        .greCard()
    }

    @ViewBuilder
    private func latestScore(_ result: ExamResult) -> some View {
        HStack(spacing: 28) {
            VStack(alignment: .leading, spacing: 3) {
                Text("LATEST RESULT")
                    .font(.caption.weight(.bold))
                    .foregroundStyle(GRETheme.teal)
                Text(result.completedAt, format: .dateTime.month(.abbreviated).day().year())
                    .font(.headline)
            }
            Divider().frame(height: 44)
            scoreChip("Verbal", result.verbalScore)
            scoreChip("Quant", result.quantitativeScore)
            if let combined = result.combinedScore { scoreChip("Combined", combined) }
            Spacer()
        }
        .greCard()
    }

    private func scoreChip(_ title: String, _ score: Int?) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            Text(score.map(String.init) ?? "—")
                .font(.title2.bold())
                .monospacedDigit()
        }
    }

    private var testCards: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Choose a session")
                .font(.title2.bold())
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 250), spacing: 16)], spacing: 16) {
                sessionCard(.fullLength, icon: "timer", accent: GRETheme.blue)
                sessionCard(.verbalPractice, icon: "text.quote", accent: GRETheme.teal)
                sessionCard(.quantitativePractice, icon: "function", accent: .indigo)
            }
        }
    }

    private func sessionCard(_ mode: ExamMode, icon: String, accent: Color) -> some View {
        VStack(alignment: .leading, spacing: 13) {
            Image(systemName: icon)
                .font(.title2.weight(.semibold))
                .foregroundStyle(accent)
                .frame(width: 44, height: 44)
                .background(accent.opacity(0.10), in: RoundedRectangle(cornerRadius: 9))
            Text(mode.title).font(.headline)
            Text(mode.subtitle).font(.subheadline).foregroundStyle(.secondary)
            Spacer(minLength: 2)
            Button("Begin") { start(mode) }
                .buttonStyle(SecondaryGREButtonStyle())
        }
        .frame(maxWidth: .infinity, minHeight: 170, alignment: .leading)
        .greCard()
    }

    private var formatOverview: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Current computer-delivered format")
                .font(.title2.bold())
            Grid(alignment: .leading, horizontalSpacing: 24, verticalSpacing: 11) {
                GridRow {
                    Text("Measure").fontWeight(.semibold)
                    Text("Section 1").fontWeight(.semibold)
                    Text("Section 2").fontWeight(.semibold)
                }
                Divider().gridCellColumns(3)
                formatRow("Analytical Writing", "1 issue task · 30 min", "—")
                formatRow("Verbal Reasoning", "12 questions · 18 min", "15 questions · 23 min")
                formatRow("Quantitative Reasoning", "12 questions · 21 min", "15 questions · 26 min")
            }
            .font(.subheadline)
        }
        .greCard()
    }

    private func formatRow(_ title: String, _ first: String, _ second: String) -> some View {
        GridRow {
            Text(title)
            Text(first).foregroundStyle(.secondary)
            Text(second).foregroundStyle(.secondary)
        }
    }

    private var accuracyNotice: some View {
        Label {
            Text("The bank combines authorized source-derived items with clearly labeled original parameterized practice. Exact source details appear only after a timed session ends. This app is not affiliated with or endorsed by ETS.")
        } icon: {
            Image(systemName: "info.circle.fill")
                .foregroundStyle(GRETheme.blue)
        }
        .font(.footnote)
        .foregroundStyle(.secondary)
    }
}
