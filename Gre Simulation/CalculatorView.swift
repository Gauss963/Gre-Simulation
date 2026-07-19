import SwiftUI
import Combine

@MainActor
private final class CalculatorModel: ObservableObject {
    @Published var display = "0"
    @Published var memory: Double = 0

    private var accumulator: Double?
    private var pendingOperation: String?
    private var startsNewNumber = true

    func input(_ key: String) {
        switch key {
        case "0"..."9": enterDigit(key)
        case ".": enterDecimal()
        case "+", "−", "×", "÷": setOperation(key)
        case "=": calculate()
        case "C": clear()
        case "±": toggleSign()
        case "%": applyUnary { $0 / 100 }
        case "√": applyUnary { sqrt(max(0, $0)) }
        case "MC": memory = 0
        case "MR": setDisplay(memory)
        case "M+": memory += currentValue
        case "M−": memory -= currentValue
        default: break
        }
    }

    private var currentValue: Double { Double(display) ?? 0 }

    private func enterDigit(_ digit: String) {
        if startsNewNumber || display == "0" {
            display = digit
            startsNewNumber = false
        } else if display.count < 13 {
            display += digit
        }
    }

    private func enterDecimal() {
        if startsNewNumber {
            display = "0."
            startsNewNumber = false
        } else if !display.contains(".") {
            display += "."
        }
    }

    private func setOperation(_ operation: String) {
        if accumulator != nil, pendingOperation != nil, !startsNewNumber { calculate() }
        accumulator = currentValue
        pendingOperation = operation
        startsNewNumber = true
    }

    private func calculate() {
        guard let accumulator, let pendingOperation else { return }
        let rhs = currentValue
        let value: Double
        switch pendingOperation {
        case "+": value = accumulator + rhs
        case "−": value = accumulator - rhs
        case "×": value = accumulator * rhs
        case "÷": value = rhs == 0 ? .nan : accumulator / rhs
        default: value = rhs
        }
        setDisplay(value)
        self.accumulator = nil
        self.pendingOperation = nil
    }

    private func toggleSign() { setDisplay(-currentValue) }

    private func applyUnary(_ transform: (Double) -> Double) {
        setDisplay(transform(currentValue))
    }

    private func setDisplay(_ value: Double) {
        if !value.isFinite {
            display = "Error"
        } else if value.rounded() == value, abs(value) < 1e12 {
            display = String(Int(value))
        } else {
            display = String(format: "%.10g", value)
        }
        startsNewNumber = true
    }

    private func clear() {
        display = "0"
        accumulator = nil
        pendingOperation = nil
        startsNewNumber = true
    }
}

struct CalculatorView: View {
    @StateObject private var model = CalculatorModel()
    @Environment(\.dismiss) private var dismiss

    private let rows = [
        ["MC", "MR", "M+", "M−"],
        ["C", "±", "%", "√"],
        ["7", "8", "9", "÷"],
        ["4", "5", "6", "×"],
        ["1", "2", "3", "−"],
        ["0", ".", "=", "+"]
    ]

    var body: some View {
        VStack(spacing: 13) {
            HStack {
                Text("Calculator").font(.headline)
                Spacer()
                Button("Close") { dismiss() }
            }
            Text(model.display)
                .font(.system(size: 32, weight: .medium, design: .monospaced))
                .lineLimit(1)
                .minimumScaleFactor(0.65)
                .frame(maxWidth: .infinity, alignment: .trailing)
                .padding(14)
                .background(GRETheme.input)
                .overlay(RoundedRectangle(cornerRadius: 5).stroke(GRETheme.navy, lineWidth: 2))

            ForEach(rows, id: \.self) { row in
                HStack(spacing: 9) {
                    ForEach(row, id: \.self) { key in
                        Button {
                            model.input(key)
                        } label: {
                            Text(key)
                                .font(.headline.monospacedDigit())
                                .frame(maxWidth: .infinity, minHeight: 43)
                                .contentShape(Rectangle())
                                .background(buttonColor(key), in: RoundedRectangle(cornerRadius: 5))
                                .foregroundStyle(buttonForeground(key))
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            Text("Basic four-function calculator provided for quantitative practice.")
                .font(.caption2)
                .foregroundStyle(.secondary)
        }
        .padding(20)
        .frame(width: 380)
        .background(GRETheme.canvas)
    }

    private func buttonColor(_ key: String) -> Color {
        if ["+", "−", "×", "÷", "="].contains(key) { return GRETheme.actionBlue }
        if ["MC", "MR", "M+", "M−"].contains(key) { return GRETheme.teal.opacity(0.18) }
        return GRETheme.input
    }

    private func buttonForeground(_ key: String) -> Color {
        ["+", "−", "×", "÷", "="].contains(key) ? .white : GRETheme.ink
    }
}
