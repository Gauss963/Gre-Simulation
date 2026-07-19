import SwiftUI

private enum VocabularyStudyMode: String, CaseIterable, Identifiable {
    case browse = "Browse"
    case flashCards = "Flash Cards"

    var id: String { rawValue }
}

struct VocabularyView: View {
    @State private var searchText = ""
    @State private var revealed: Set<String> = []
    @State private var selectedSource = "All sources"
    @State private var studyMode: VocabularyStudyMode = .browse

    init() {
        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("-openFlashcards") {
            _studyMode = State(initialValue: .flashCards)
        }
        #endif
    }

    private var filteredWords: [VocabularyWord] {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return VocabularyBank.words.filter {
            let matchesSource = selectedSource == "All sources" || $0.sources.contains(selectedSource)
            let matchesQuery = query.isEmpty
                || $0.word.lowercased().contains(query)
                || $0.definition.lowercased().contains(query)
                || $0.chinese.contains(query)
                || $0.synonyms.contains(where: { $0.lowercased().contains(query) })
            return matchesSource && matchesQuery
        }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                VStack(alignment: .leading, spacing: 7) {
                    Text("High-frequency vocabulary lab")
                        .font(.system(size: 30, weight: .bold, design: .rounded))
                        .foregroundStyle(GRETheme.navy)
                    Text("Browse the authorized offline word bank or work through shuffled flash-card sessions with persistent mastery progress.")
                        .foregroundStyle(.secondary)
                }

                Picker("Study mode", selection: $studyMode) {
                    ForEach(VocabularyStudyMode.allCases) { mode in
                        Text(mode.rawValue).tag(mode)
                    }
                }
                .pickerStyle(.segmented)
                .frame(maxWidth: 360)

                if studyMode == .browse {
                    ViewThatFits {
                        HStack(spacing: 12) { searchField; sourcePicker }
                        VStack(spacing: 10) { searchField; sourcePicker }
                    }

                    LazyVGrid(columns: [GridItem(.adaptive(minimum: 285), spacing: 14)], spacing: 14) {
                        ForEach(filteredWords) { item in
                            vocabularyCard(item)
                        }
                    }

                    Text("Word entries retain source tags after deduplication. Chinese definitions use Taiwan Traditional Chinese. Definitions may be compacted for card display; consult the authorized source material for complete study notes.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                        .padding(.top, 6)
                } else {
                    FlashcardStudyView(words: VocabularyBank.words)
                }
            }
            .frame(maxWidth: 1050, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Vocabulary")
    }

    private var searchField: some View {
        HStack {
            Image(systemName: "magnifyingglass").foregroundStyle(.secondary)
            TextField("Search words, meanings, or synonyms", text: $searchText)
                .textFieldStyle(.plain)
            Text("\(filteredWords.count) words")
                .font(.caption.monospacedDigit())
                .foregroundStyle(.secondary)
        }
        .padding(12)
        .background(.background, in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(Color.primary.opacity(0.09)))
    }

    private var sourcePicker: some View {
        Picker("Source", selection: $selectedSource) {
            Text("All sources").tag("All sources")
            ForEach(VocabularyBank.sourceOptions, id: \.self) { Text($0).tag($0) }
        }
        .pickerStyle(.menu)
        .frame(minWidth: 190)
        .padding(.horizontal, 8)
        .padding(.vertical, 5)
        .background(.background, in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(Color.primary.opacity(0.09)))
    }

    private func vocabularyCard(_ item: VocabularyWord) -> some View {
        Button {
            withAnimation(.easeInOut(duration: 0.18)) {
                if revealed.contains(item.id) { revealed.remove(item.id) } else { revealed.insert(item.id) }
            }
        } label: {
            VStack(alignment: .leading, spacing: 10) {
                HStack(alignment: .firstTextBaseline) {
                    Text(item.word)
                        .font(.title2.bold())
                        .foregroundStyle(GRETheme.navy)
                    if !item.pronunciation.isEmpty {
                        Text(item.pronunciation)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                    Image(systemName: revealed.contains(item.id) ? "eye.fill" : "eye.slash")
                        .foregroundStyle(GRETheme.blue)
                }
                if revealed.contains(item.id) {
                    Text(item.definition)
                        .font(.body.weight(.medium))
                        .foregroundStyle(.primary)
                    if !item.chinese.isEmpty {
                        Text(item.chinese).foregroundStyle(GRETheme.teal)
                    }
                    if !item.synonyms.isEmpty {
                        Text(item.synonyms.joined(separator: " · "))
                            .font(.caption.weight(.semibold))
                            .foregroundStyle(.secondary)
                    }
                    if !item.example.isEmpty {
                        Text(item.example)
                            .font(.subheadline)
                            .italic()
                            .foregroundStyle(.secondary)
                        if let source = item.exampleSource, !source.isEmpty {
                            if let urlString = item.exampleSourceURL,
                               let url = URL(string: urlString) {
                                Link(destination: url) {
                                    Label(source, systemImage: "arrow.up.right.square")
                                        .font(.caption2)
                                }
                            } else {
                                Text(source)
                                    .font(.caption2)
                                    .foregroundStyle(.tertiary)
                            }
                        }
                    }
                    if !item.sources.isEmpty {
                        Text(item.sources.joined(separator: " · "))
                            .font(.caption2)
                            .foregroundStyle(.tertiary)
                    }
                } else {
                    Text("Tap to reveal meaning")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                    Spacer(minLength: 52)
                }
            }
            .frame(maxWidth: .infinity, minHeight: 165, alignment: .topLeading)
            .greCard()
        }
        .buttonStyle(.plain)
    }
}

struct HistoryView: View {
    @ObservedObject var store: HistoryStore
    @State private var confirmClear = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                HStack {
                    VStack(alignment: .leading, spacing: 6) {
                        Text("Score history")
                            .font(.system(size: 30, weight: .bold, design: .rounded))
                            .foregroundStyle(GRETheme.navy)
                        Text("Results stay on this device.")
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                    if !store.results.isEmpty {
                        Button("Clear history", role: .destructive) { confirmClear = true }
                    }
                }

                if store.results.isEmpty {
                    ContentUnavailableView(
                        "No completed tests",
                        systemImage: "chart.line.uptrend.xyaxis",
                        description: Text("Complete a timed practice session to see scores and section performance here.")
                    )
                    .frame(maxWidth: .infinity, minHeight: 420)
                    .greCard()
                } else {
                    ForEach(store.results) { result in
                        historyCard(result)
                    }
                }
            }
            .frame(maxWidth: 950, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Score History")
        .confirmationDialog("Delete every saved result?", isPresented: $confirmClear, titleVisibility: .visible) {
            Button("Delete all results", role: .destructive) { store.clear() }
        }
    }

    private func historyCard(_ result: ExamResult) -> some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 3) {
                    Text(result.mode.title).font(.headline)
                    Text(result.completedAt, format: .dateTime.year().month().day().hour().minute())
                        .font(.caption).foregroundStyle(.secondary)
                }
                Spacer()
                Text(duration(result.elapsedSeconds))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(.secondary)
            }

            HStack(spacing: 12) {
                resultMetric("Verbal", result.verbalScore.map(String.init) ?? "—", "\(result.verbalCorrect)/\(result.verbalTotal) correct")
                resultMetric("Quant", result.quantitativeScore.map(String.init) ?? "—", "\(result.quantitativeCorrect)/\(result.quantitativeTotal) correct")
                if let writing = result.writingEstimate {
                    resultMetric("Writing", String(format: "%.1f", writing), "practice signal")
                }
            }

            if !result.sections.isEmpty {
                Divider()
                ViewThatFits {
                    HStack(spacing: 12) { sectionChips(result.sections) }
                    VStack(alignment: .leading, spacing: 8) { sectionChips(result.sections) }
                }
            }
        }
        .greCard()
    }

    private func sectionChips(_ sections: [SectionSummary]) -> some View {
        ForEach(Array(sections.enumerated()), id: \.offset) { _, section in
            HStack(spacing: 6) {
                Text("\(section.measure.shortTitle) \(section.ordinal)").fontWeight(.semibold)
                Text("\(section.correct)/\(section.total)")
                if let difficulty = section.difficulty { Text("· \(difficulty.title)") }
            }
            .font(.caption)
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(GRETheme.canvas, in: Capsule())
        }
    }

    private func resultMetric(_ title: String, _ value: String, _ subtitle: String) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            Text(value).font(.title.bold()).monospacedDigit().foregroundStyle(GRETheme.navy)
            Text(subtitle).font(.caption2).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(14)
        .background(GRETheme.canvas.opacity(0.8), in: RoundedRectangle(cornerRadius: 8))
    }

    private func duration(_ seconds: Int) -> String {
        let hours = seconds / 3600
        let minutes = (seconds % 3600) / 60
        let remaining = seconds % 60
        return hours > 0
            ? String(format: "%d:%02d:%02d", hours, minutes, remaining)
            : String(format: "%02d:%02d", minutes, remaining)
    }
}

struct ResourcesView: View {
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Authoritative preparation resources")
                        .font(.system(size: 30, weight: .bold, design: .rounded))
                        .foregroundStyle(GRETheme.navy)
                    Text("Use official ETS material for the test specification and authentic sample items. Use third-party lists only as supplementary study aids.")
                        .foregroundStyle(.secondary)
                }

                VStack(alignment: .leading, spacing: 12) {
                    Label("Offline content bank", systemImage: "externaldrive.fill.badge.checkmark")
                        .font(.title3.bold())
                        .foregroundStyle(GRETheme.navy)
                    HStack(spacing: 14) {
                        resultMetric("Questions", "\(QuestionBank.all.count)", "Verbal + Quant")
                        resultMetric("Vocabulary", "\(VocabularyBank.words.count)", "deduplicated entries")
                    }
                    ForEach(Array(QuestionBank.sourceSummary.enumerated()), id: \.offset) { _, item in
                        HStack {
                            Text(item.title).font(.subheadline)
                            Spacer()
                            Text("\(item.count)").font(.subheadline.monospacedDigit()).foregroundStyle(.secondary)
                        }
                    }
                    Text("Source-derived and generated items are labeled separately in the post-test answer review.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .greCard()

                resourceSection("Official test format", icon: "clock.badge.checkmark", links: [
                    ResourceLink(title: "GRE General Test Content & Structure", detail: "Current section counts and timing", url: "https://www.ets.org/gre/score-users/about/general-test/content-structure.html"),
                    ResourceLink(title: "Understanding GRE Scores", detail: "Section adaptation, raw scores, and equating", url: "https://www.ets.org/gre/test-takers/general-test/scores/understand-scores.html"),
                    ResourceLink(title: "POWERPREP Practice Tests", detail: "Official computer-delivered practice experience", url: "https://www.ets.org/gre/test-takers/general-test/prepare/powerprep.html")
                ])

                resourceSection("Quantitative reasoning", icon: "function", links: [
                    ResourceLink(title: "Official Quantitative Reasoning Overview", detail: "Tested topics and all four question formats", url: "https://www.ets.org/gre/test-takers/general-test/prepare/content/quantitative-reasoning.html"),
                    ResourceLink(title: "Official GRE Math Review", detail: "Arithmetic, algebra, geometry, and data analysis", url: "https://www.ets.org/content/dam/ets-org/pdfs/gre/gre-math-review.pdf"),
                    ResourceLink(title: "Khan Academy for GRE Quant", detail: "Free instruction mapped to the ETS Math Review", url: "https://www.ets.org/gre/test-takers/general-test/prepare/khan-prep-videos.html")
                ])

                resourceSection("Verbal and vocabulary", icon: "text.book.closed", links: [
                    ResourceLink(title: "Official Verbal Reasoning Overview", detail: "Reading comprehension, text completion, and sentence equivalence", url: "https://www.ets.org/gre/test-takers/general-test/prepare/content/verbal-reasoning.html"),
                    ResourceLink(title: "liurui39660/3000", detail: "Authorized vocabulary workbook used by this build", url: "https://github.com/liurui39660/3000"),
                    ResourceLink(title: "Open English WordNet 2025", detail: "CC BY 4.0 definitions for unmatched Gauss terms", url: "https://en-word.net/"),
                    ResourceLink(title: "ECDICT", detail: "MIT-licensed Chinese lexical supplement", url: "https://github.com/skywind3000/ECDICT"),
                    ResourceLink(title: "Tatoeba Corpus", detail: "CC0 and CC BY 2.0 FR English example sentences", url: "https://tatoeba.org/en/downloads")
                ])

                VStack(alignment: .leading, spacing: 10) {
                    Label("Content and score transparency", systemImage: "shield.lefthalf.filled")
                        .font(.headline)
                        .foregroundStyle(GRETheme.navy)
                    Text("ETS uses proprietary equating that accounts for test-edition difficulty and the adaptive route. Because ETS does not publish that conversion table, this app reports a linear 130–170 practice estimate from raw accuracy. The writing estimate is a local structural heuristic, not e-rater® or an official GRE score.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                    Text("GRE®, POWERPREP®, and e-rater® are trademarks of ETS. This independent app is not affiliated with or endorsed by ETS.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .greCard()
            }
            .frame(maxWidth: 950, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Official Resources")
    }

    private func resultMetric(_ title: String, _ value: String, _ detail: String) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            Text(value).font(.title.bold()).monospacedDigit().foregroundStyle(GRETheme.blue)
            Text(detail).font(.caption2).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(GRETheme.canvas, in: RoundedRectangle(cornerRadius: 8))
    }

    private func resourceSection(_ title: String, icon: String, links: [ResourceLink]) -> some View {
        VStack(alignment: .leading, spacing: 13) {
            Label(title, systemImage: icon)
                .font(.title3.bold())
                .foregroundStyle(GRETheme.navy)
            ForEach(links) { item in
                Link(destination: URL(string: item.url)!) {
                    HStack(spacing: 12) {
                        VStack(alignment: .leading, spacing: 3) {
                            Text(item.title).fontWeight(.semibold).foregroundStyle(.primary)
                            Text(item.detail).font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Image(systemName: "arrow.up.right.square")
                            .foregroundStyle(GRETheme.blue)
                    }
                    .padding(.vertical, 5)
                }
                .buttonStyle(.plain)
                if item.id != links.last?.id { Divider() }
            }
        }
        .greCard()
    }
}

private struct ResourceLink: Identifiable {
    let title: String
    let detail: String
    let url: String
    var id: String { url }
}
