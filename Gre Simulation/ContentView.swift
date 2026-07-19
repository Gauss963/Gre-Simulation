import SwiftUI
import Foundation
#if os(macOS)
import AppKit
#endif

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
        if ProcessInfo.processInfo.arguments.contains("-startFullPractice") {
            _activeExamMode = State(initialValue: .fullLength)
        } else if ProcessInfo.processInfo.arguments.contains("-startVerbalPractice") {
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
                    .background(MacExamFullScreenController())
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
                    .fill(GRETheme.blue)
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

#if os(macOS)
private struct MacExamFullScreenController: NSViewRepresentable {
    final class Coordinator {
        weak var window: NSWindow?
        private var enteredFullScreen = false

        func attach(to window: NSWindow) {
            self.window = window
            guard !enteredFullScreen, !window.styleMask.contains(.fullScreen) else { return }
            enteredFullScreen = true
            window.toggleFullScreen(nil)
        }

        func restoreWindowedMode() {
            guard enteredFullScreen else { return }
            enteredFullScreen = false
            DispatchQueue.main.async { [weak window] in
                guard let window, window.styleMask.contains(.fullScreen) else { return }
                window.toggleFullScreen(nil)
            }
        }
    }

    final class WindowTrackingView: NSView {
        var didAttachToWindow: ((NSWindow) -> Void)?

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            if let window {
                didAttachToWindow?(window)
            }
        }
    }

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    func makeNSView(context: Context) -> NSView {
        let view = WindowTrackingView()
        view.didAttachToWindow = { window in
            context.coordinator.attach(to: window)
        }
        return view
    }

    func updateNSView(_ nsView: NSView, context: Context) {}

    static func dismantleNSView(_ nsView: NSView, coordinator: Coordinator) {
        coordinator.restoreWindowedMode()
    }
}
#endif

#Preview {
    ContentView()
}
