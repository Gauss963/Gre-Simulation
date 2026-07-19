import SwiftUI

enum GRETheme {
    static let navy = Color(red: 0.055, green: 0.17, blue: 0.27)
    static let blue = Color(red: 0.05, green: 0.32, blue: 0.54)
    static let brightBlue = Color(red: 0.05, green: 0.45, blue: 0.72)
    static let teal = Color(red: 0.08, green: 0.52, blue: 0.56)
    static let canvas = Color(red: 0.94, green: 0.95, blue: 0.96)
    static let border = Color(red: 0.77, green: 0.80, blue: 0.82)
    static let ink = Color(red: 0.10, green: 0.13, blue: 0.16)
    static let warning = Color(red: 0.78, green: 0.34, blue: 0.08)
}

struct PrimaryGREButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.headline)
            .foregroundStyle(.white)
            .padding(.horizontal, 22)
            .padding(.vertical, 12)
            .background(configuration.isPressed ? GRETheme.navy : GRETheme.blue)
            .clipShape(RoundedRectangle(cornerRadius: 5))
    }
}

struct SecondaryGREButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.headline)
            .foregroundStyle(GRETheme.navy)
            .padding(.horizontal, 18)
            .padding(.vertical, 10)
            .background(configuration.isPressed ? Color.gray.opacity(0.18) : .white)
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(GRETheme.border))
            .clipShape(RoundedRectangle(cornerRadius: 5))
    }
}

extension View {
    func greCard() -> some View {
        padding(20)
            .background(.background)
            .clipShape(RoundedRectangle(cornerRadius: 12))
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.primary.opacity(0.08)))
            .shadow(color: .black.opacity(0.04), radius: 8, y: 3)
    }
}
