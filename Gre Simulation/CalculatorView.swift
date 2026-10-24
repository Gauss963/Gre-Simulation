import SwiftUI
import Combine

/// The GRE on-screen calculator: order of operations, parentheses, memory, square root,
/// CE/C, and Transfer Display for Numeric Entry questions.
@MainActor
final class CalculatorModel: ObservableObject {
    @Published private(set) var display = "0"
    @Published private(set) var expression = ""
    @Published private(set) var memory: Double = 0
    @Published private(set) var hasError = false

    private var tokens: [String] = []
    private var entry = ""
    private var justEvaluated = false

    func input(_ key: String) {
        if hasError, key != "C" { return }
        switch key {
        case "0"..."9": digit(key)
        case ".": decimal()
        case "+", "−", "×", "÷": binary(key)
        case "(": openParenthesis()
        case ")": closeParenthesis()
        case "=": evaluate()
        case "C": clear()
        case "CE": entry = ""
        case "±": negate()
        case "√": squareRoot()
        case "MC": memory = 0
        case "MR": entry = Self.format(memory)
        case "M+":
            let total = memory + currentValue
            if abs(total) > 99_999_999 { tokens = []; entry = ""; hasError = true; display = "ERROR" } else { memory = total }
        default: break
        }
        refresh()
    }

    var currentValue: Double { Double(entry.isEmpty ? display : entry) ?? 0 }

    /// The value written into a Numeric Entry box by Transfer Display.
    var transferText: String { hasError ? "" : display }

    private func digit(_ digit: String) {
        if justEvaluated { tokens = []; entry = ""; justEvaluated = false }
        if entry == "0" { entry = digit } else if entry.filter(\.isNumber).count < 8 { entry += digit }
    }

    private func decimal() {
        if justEvaluated { tokens = []; entry = ""; justEvaluated = false }
        if entry.isEmpty { entry = "0." } else if !entry.contains(".") { entry += "." }
    }

    private func binary(_ op: String) {
        justEvaluated = false
        if !entry.isEmpty {
            tokens.append(entry)
            entry = ""
        } else if let last = tokens.last, ["+", "−", "×", "÷"].contains(last) {
            tokens.removeLast()
        } else if tokens.isEmpty || tokens.last == "(" {
            tokens.append(display)
        }
        tokens.append(op)
    }

    private func openParenthesis() {
        if justEvaluated { tokens = []; justEvaluated = false }
        guard entry.isEmpty else { return }
        tokens.append("(")
    }

    private func closeParenthesis() {
        let opens = tokens.filter { $0 == "(" }.count, closes = tokens.filter { $0 == ")" }.count
        guard opens > closes else { return }
        if !entry.isEmpty { tokens.append(entry); entry = "" }
        tokens.append(")")
    }

    private func evaluate() {
        if !entry.isEmpty { tokens.append(entry); entry = "" }
        while let last = tokens.last, ["+", "−", "×", "÷", "("].contains(last) { tokens.removeLast() }
        let missing = tokens.filter { $0 == "(" }.count - tokens.filter { $0 == ")" }.count
        tokens += Array(repeating: ")", count: max(0, missing))
        guard !tokens.isEmpty else { return }
        if let value = Self.evaluate(tokens), value.isFinite, abs(value) <= 99_999_999 {
            entry = Self.format(value)
            tokens = []
            justEvaluated = true
        } else {
            tokens = []; entry = ""
            hasError = true
            display = "ERROR"
        }
    }

    private func clear() {
        tokens = []; entry = ""; display = "0"; hasError = false; justEvaluated = false
    }

    private func negate() {
        if entry.isEmpty { entry = display }
        if entry == "0" { return }
        entry = entry.hasPrefix("-") ? String(entry.dropFirst()) : "-" + entry
    }

    private func squareRoot() {
        let value = currentValue
        guard value >= 0 else { tokens = []; entry = ""; hasError = true; display = "ERROR"; return }
        entry = Self.format(sqrt(value))
    }

    private func refresh() {
        guard !hasError else { return }
        if !entry.isEmpty {
            display = entry
        } else if let last = tokens.last(where: { Double($0) != nil }) {
            display = last
        } else if tokens.isEmpty, !justEvaluated {
            display = "0"
        }
        expression = tokens.joined(separator: " ")
    }

    /// Shows at most 8 digits, rounding the decimal part to fit (as the GRE calculator does).
    static func format(_ value: Double) -> String {
        if value.rounded() == value, abs(value) <= 99_999_999 { return String(Int64(value)) }
        let integerDigits = max(1, String(Int64(abs(value).rounded(.down))).count)
        let decimals = max(0, 8 - integerDigits)
        var text = String(format: "%.\(decimals)f", value)
        if text.contains(".") {
            while text.hasSuffix("0") { text.removeLast() }
            if text.hasSuffix(".") { text.removeLast() }
        }
        return text == "-0" ? "0" : text
    }

    /// Shunting-yard evaluation with standard precedence.
    static func evaluate(_ tokens: [String]) -> Double? {
        var output: [Double] = []
        var operators: [String] = []
        let precedence = ["+": 1, "−": 1, "×": 2, "÷": 2]
        func apply() -> Bool {
            guard let op = operators.popLast(), output.count >= 2 else { return false }
            let b = output.removeLast(), a = output.removeLast()
            switch op {
            case "+": output.append(a + b)
            case "−": output.append(a - b)
            case "×": output.append(a * b)
            case "÷":
                guard b != 0 else { return false }
                output.append(a / b)
            default: return false
            }
            return true
        }
        for token in tokens {
            if let number = Double(token) {
                output.append(number)
            } else if token == "(" {
                operators.append(token)
            } else if token == ")" {
                while let last = operators.last, last != "(" { guard apply() else { return nil } }
                guard operators.popLast() == "(" else { return nil }
            } else if let p = precedence[token] {
                while let last = operators.last, let q = precedence[last], q >= p { guard apply() else { return nil } }
                operators.append(token)
            }
        }
        while !operators.isEmpty { guard apply() else { return nil } }
        return output.count == 1 ? output[0] : nil
    }
}

/// A floating calculator panel that can be dragged and does not cover the question.
struct CalculatorView: View {
    @ObservedObject var model: CalculatorModel
    let canTransfer: Bool
    let transfer: (String) -> Void
    let close: () -> Void

    // Key layout of the official GRE calculator.
    private let rows = [
        ["MR", "MC", "M+", "(", ")"],
        ["7", "8", "9", "÷", "C"],
        ["4", "5", "6", "×", "CE"],
        ["1", "2", "3", "−", "√"],
        ["±", "0", ".", "+", "="]
    ]

    var body: some View {
        VStack(spacing: 10) {
            HStack {
                Image(systemName: "line.3.horizontal").foregroundStyle(.secondary)
                Text("Calculator").font(.headline)
                Spacer()
                Button { close() } label: { Image(systemName: "xmark.circle.fill").font(.title3) }
                    .buttonStyle(.plain)
                    .accessibilityLabel("Close calculator")
            }
            VStack(alignment: .trailing, spacing: 2) {
                Text(model.expression.isEmpty ? " " : model.expression)
                    .font(.caption.monospaced()).foregroundStyle(.secondary).lineLimit(1)
                HStack {
                    if model.memory != 0 { Text("M").font(.caption.bold()).foregroundStyle(GRETheme.blue) }
                    Spacer()
                    Text(model.display)
                        .font(.system(size: 28, weight: .medium, design: .monospaced))
                        .lineLimit(1).minimumScaleFactor(0.6)
                }
            }
            .padding(10)
            .background(GRETheme.input)
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(GRETheme.navy, lineWidth: 2))
            ForEach(rows, id: \.self) { row in
                HStack(spacing: 7) {
                    ForEach(row, id: \.self) { key in
                        Button { model.input(key) } label: {
                            Text(key)
                                .font(.headline.monospacedDigit())
                                .frame(maxWidth: .infinity, minHeight: 38)
                                .contentShape(Rectangle())
                                .background(buttonColor(key), in: RoundedRectangle(cornerRadius: 5))
                                .foregroundStyle(key == "=" ? Color.white : GRETheme.ink)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            Button { transfer(model.transferText) } label: {
                Text("Transfer Display")
                    .font(.headline)
                    .frame(maxWidth: .infinity, minHeight: 34)
                    .background(GRETheme.surface, in: RoundedRectangle(cornerRadius: 5))
                    .foregroundStyle(canTransfer ? GRETheme.ink : Color.secondary)
            }
            .buttonStyle(.plain)
            .disabled(!canTransfer)
        }
        .padding(14)
        .frame(width: 330)
        .background(GRETheme.canvas, in: RoundedRectangle(cornerRadius: 10))
        .overlay(RoundedRectangle(cornerRadius: 10).stroke(GRETheme.border))
        .shadow(color: .black.opacity(0.25), radius: 14, y: 6)
    }

    private func buttonColor(_ key: String) -> Color {
        if key == "=" { return GRETheme.actionBlue }
        if Int(key) != nil || key == "." { return GRETheme.input }
        return GRETheme.surface
    }
}
