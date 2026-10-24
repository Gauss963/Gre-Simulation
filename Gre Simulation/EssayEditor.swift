import SwiftUI
import Combine
#if os(macOS)
import AppKit
#else
import UIKit
#endif

/// Commands for the GRE word processor toolbar (Cut, Paste, Undo, Redo).
@MainActor
final class EssayEditorController: ObservableObject {
    #if os(macOS)
    fileprivate weak var textView: NSTextView?
    #else
    fileprivate weak var textView: UITextView?
    #endif

    func cut() {
        #if os(macOS)
        textView?.cut(nil)
        #else
        textView?.cut(nil)
        #endif
    }

    func paste() {
        #if os(macOS)
        textView?.pasteAsPlainText(nil)
        #else
        textView?.paste(nil)
        #endif
    }

    func undo() { textView?.undoManager?.undo() }
    func redo() { textView?.undoManager?.redo() }
}

/// A plain-text editor like the GRE's: no spell check, grammar check, autocorrect, or substitutions.
struct EssayEditor: View {
    @Binding var text: String
    @ObservedObject var controller: EssayEditorController

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                Button("Cut") { controller.cut() }
                Button("Paste") { controller.paste() }
                Button("Undo") { controller.undo() }
                Button("Redo") { controller.redo() }
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            PlainTextView(text: $text, controller: controller)
                .frame(minHeight: 380)
                .overlay(RoundedRectangle(cornerRadius: 4).stroke(GRETheme.navy, lineWidth: 1.5))
        }
    }
}

#if os(macOS)
private struct PlainTextView: NSViewRepresentable {
    @Binding var text: String
    let controller: EssayEditorController

    func makeNSView(context: Context) -> NSScrollView {
        let scrollView = NSTextView.scrollableTextView()
        guard let textView = scrollView.documentView as? NSTextView else { return scrollView }
        textView.delegate = context.coordinator
        textView.isRichText = false
        textView.allowsUndo = true
        textView.isContinuousSpellCheckingEnabled = false
        textView.isGrammarCheckingEnabled = false
        textView.isAutomaticSpellingCorrectionEnabled = false
        textView.isAutomaticTextReplacementEnabled = false
        textView.isAutomaticQuoteSubstitutionEnabled = false
        textView.isAutomaticDashSubstitutionEnabled = false
        textView.isAutomaticTextCompletionEnabled = false
        textView.font = NSFont(name: "Georgia", size: 16) ?? .systemFont(ofSize: 16)
        textView.textContainerInset = NSSize(width: 8, height: 10)
        textView.string = text
        controller.textView = textView
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        guard let textView = scrollView.documentView as? NSTextView else { return }
        if textView.string != text { textView.string = text }
        controller.textView = textView
    }

    func makeCoordinator() -> Coordinator { Coordinator(text: $text) }

    final class Coordinator: NSObject, NSTextViewDelegate {
        let text: Binding<String>
        init(text: Binding<String>) { self.text = text }

        func textDidChange(_ notification: Notification) {
            guard let textView = notification.object as? NSTextView else { return }
            text.wrappedValue = textView.string
        }
    }
}
#else
private struct PlainTextView: UIViewRepresentable {
    @Binding var text: String
    let controller: EssayEditorController

    func makeUIView(context: Context) -> UITextView {
        let textView = UITextView()
        textView.delegate = context.coordinator
        textView.spellCheckingType = .no
        textView.autocorrectionType = .no
        textView.autocapitalizationType = .sentences
        textView.smartQuotesType = .no
        textView.smartDashesType = .no
        textView.smartInsertDeleteType = .no
        textView.inlinePredictionType = .no
        textView.font = UIFont(name: "Georgia", size: 17) ?? .systemFont(ofSize: 17)
        textView.text = text
        controller.textView = textView
        return textView
    }

    func updateUIView(_ textView: UITextView, context: Context) {
        if textView.text != text { textView.text = text }
        controller.textView = textView
    }

    func makeCoordinator() -> Coordinator { Coordinator(text: $text) }

    final class Coordinator: NSObject, UITextViewDelegate {
        let text: Binding<String>
        init(text: Binding<String>) { self.text = text }

        func textViewDidChange(_ textView: UITextView) {
            text.wrappedValue = textView.text
        }
    }
}
#endif
