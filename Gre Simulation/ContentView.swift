import SwiftUI
import Foundation

enum AppPage: String, CaseIterable, Identifiable {
    case dashboard
    case vocabulary
    case history
    case resources

    var id: String { rawValue }

    var title: String {
        switch self {
        case .dashboard: "Practice Tests"
        case .vocabulary: "Vocabulary"
        case .history: "Score History"
        case .resources: "Official Resources"
        }
    }

    var icon: String {
        switch self {
        case .dashboard: "rectangle.and.pencil.and.ellipsis"
        case .vocabulary: "text.book.closed"
        case .history: "chart.line.uptrend.xyaxis"
        case .resources: "checkmark.seal"
        }
    }
}

struct ContentView: View {
    @StateObject private var history = HistoryStore()
    @State private var selection: AppPage? = .dashboard
    @State private var activeExamMode: ExamMode?

    init() {
        #if DEBUG
        let arguments = ProcessInfo.processInfo.arguments
        if arguments.contains("-openVocabulary") {
            _selection = State(initialValue: .vocabulary)
        }
        if arguments.contains("-startFullPractice") {
            _activeExamMode = State(initialValue: .fullLength)
        } else if arguments.contains("-startVerbalPractice") {
            _activeExamMode = State(initialValue: .verbalPractice)
        }
        #endif
    }

    var body: some View {
        #if os(macOS)
        Group {
            if let activeExamMode {
                examDestination(activeExamMode)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(GRETheme.canvas.ignoresSafeArea())
            } else {
                rootContent
            }
        }
        #else
        rootContent
            .fullScreenCover(item: $activeExamMode, content: examDestination)
        #endif
    }

    private var rootContent: some View {
        NavigationSplitView {
            VStack(spacing: 0) {
                brand
                List(AppPage.allCases, selection: $selection) { page in
                    Label(page.title, systemImage: page.icon)
                        .tag(page)
                        .padding(.vertical, 4)
                }
                .listStyle(.sidebar)

                Text("Independent GRE® practice simulator")
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .padding(14)
            }
            .navigationSplitViewColumnWidth(min: 220, ideal: 250, max: 300)
        } detail: {
            Group {
                switch selection ?? .dashboard {
                case .dashboard:
                    DashboardView(history: history.results, start: { activeExamMode = $0 })
                case .vocabulary:
                    VocabularyView()
                case .history:
                    HistoryView(store: history)
                case .resources:
                    ResourcesView()
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(GRETheme.canvas)
        }
        .navigationSplitViewStyle(.balanced)
    }

    @ViewBuilder
    private func examDestination(_ mode: ExamMode) -> some View {
        ExamContainerView(mode: mode) { result in
            history.add(result)
            activeExamMode = nil
            selection = .history
        } onExit: {
            activeExamMode = nil
        }
    }

    private var brand: some View {
        HStack(spacing: 11) {
            ZStack {
                RoundedRectangle(cornerRadius: 8)
                    .fill(GRETheme.actionBlue)
                    .frame(width: 38, height: 38)
                Text("G")
                    .font(.system(size: 21, weight: .bold, design: .serif))
                    .foregroundStyle(.white)
            }
            VStack(alignment: .leading, spacing: 1) {
                Text("GRE Simulator")
                    .font(.headline)
                Text("Mac + iPad")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
        }
        .padding(16)
    }
}

#Preview {
    ContentView()
}
