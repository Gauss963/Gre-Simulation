import SwiftUI
#if os(macOS)
import AppKit
#else
import UIKit
#endif

enum GRETheme {
    static let headerNavy = Color(red: 0.055, green: 0.17, blue: 0.27)
    static let actionBlue = Color(red: 0.05, green: 0.32, blue: 0.54)
    static let navy = adaptive(
        light: (0.055, 0.17, 0.27, 1),
        dark: (0.66, 0.84, 0.96, 1)
    )
    static let blue = adaptive(
        light: (0.05, 0.32, 0.54, 1),
        dark: (0.35, 0.70, 0.94, 1)
    )
    static let brightBlue = adaptive(
        light: (0.05, 0.45, 0.72, 1),
        dark: (0.36, 0.78, 0.98, 1)
    )
    static let teal = adaptive(
        light: (0.08, 0.52, 0.56, 1),
        dark: (0.38, 0.82, 0.79, 1)
    )
    static let warning = adaptive(
        light: (0.78, 0.34, 0.08, 1),
        dark: (1.0, 0.64, 0.36, 1)
    )

    #if os(macOS)
    static let canvas = Color(nsColor: .windowBackgroundColor)
    static let surface = Color(nsColor: .controlBackgroundColor)
    static let input = Color(nsColor: .textBackgroundColor)
    static let border = Color(nsColor: .separatorColor)
    static let ink = Color(nsColor: .labelColor)
    #else
    static let canvas = Color(uiColor: .systemGroupedBackground)
    static let surface = Color(uiColor: .secondarySystemGroupedBackground)
    static let input = Color(uiColor: .secondarySystemBackground)
    static let border = Color(uiColor: .separator)
    static let ink = Color(uiColor: .label)
    #endif

    private typealias RGBA = (red: CGFloat, green: CGFloat, blue: CGFloat, alpha: CGFloat)

    private static func adaptive(light: RGBA, dark: RGBA) -> Color {
        #if os(macOS)
        let color = NSColor(name: nil) { appearance in
            let components = appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? dark : light
            return NSColor(
                srgbRed: components.red,
                green: components.green,
                blue: components.blue,
                alpha: components.alpha
            )
        }
        return Color(nsColor: color)
        #else
        return Color(uiColor: UIColor { traits in
            let components = traits.userInterfaceStyle == .dark ? dark : light
            return UIColor(
                red: components.red,
                green: components.green,
                blue: components.blue,
                alpha: components.alpha
            )
        })
        #endif
    }
}

struct PrimaryGREButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.headline)
            .foregroundStyle(.white)
            .padding(.horizontal, 22)
            .padding(.vertical, 12)
            .background(configuration.isPressed ? GRETheme.headerNavy : GRETheme.actionBlue)
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
            .background(configuration.isPressed ? Color.gray.opacity(0.22) : GRETheme.surface)
            .overlay(RoundedRectangle(cornerRadius: 5).stroke(GRETheme.border))
            .clipShape(RoundedRectangle(cornerRadius: 5))
    }
}

extension View {
    func greCard() -> some View {
        padding(20)
            .background(GRETheme.surface)
            .clipShape(RoundedRectangle(cornerRadius: 12))
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.primary.opacity(0.08)))
            .shadow(color: .black.opacity(0.04), radius: 8, y: 3)
    }
}
