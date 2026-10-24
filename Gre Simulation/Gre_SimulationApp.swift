//
//  Gre_SimulationApp.swift
//  Gre Simulation
//
//  Created by Gauss on 7/19/26.
//

import SwiftUI

@main
struct Gre_SimulationApp: App {
    init() {
        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("-selfTest") { SelfTest.run() }
        let issues = QuestionBank.validate() + VocabularyBank.validate() + FormBuilder.validate()
        issues.forEach { print("Validation:", $0) }
        assert(issues.isEmpty, issues.joined(separator: "\n"))
        assert(NumericAnswerMatcher.matches("2.50", "5/2"))
        assert(!NumericAnswerMatcher.matches("2.51", "5/2"))
        #endif
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
        }
        #if os(macOS)
        .defaultSize(width: 1180, height: 760)
        .windowResizability(.contentMinSize)
        #endif
    }
}
