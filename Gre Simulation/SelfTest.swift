#if DEBUG
import Foundation

/// Debug-only check of generated forms, routing, scoring, the calculator, and essay scoring.
/// Run the Debug build with `-selfTest`; results go to standard error.
@MainActor
enum SelfTest {
    static func run() -> Never {
        var failures: [String] = []
        func log(_ text: String) { FileHandle.standardError.write(Data((text + "\n").utf8)) }
        func check(_ condition: Bool, _ message: String) { if !condition { failures.append(message) } }

        let validation = QuestionBank.validate() + VocabularyBank.validate() + FormBuilder.validate()
        validation.forEach { failures.append("Validation: " + $0) }

        for run in 0..<12 {
            let session = ExamSession(mode: .fullLength)
            let layout = session.sections.map { "\($0.measure.shortTitle)\($0.ordinal)[\($0.questions.count)q/\($0.durationSeconds / 60)m]" }
            if run < 3 { log("Form \(run + 1): " + layout.joined(separator: " → ")) }
            check(session.sections.first?.measure == .analyticalWriting, "Writing is not first")
            check(session.sections.count == 5, "Full test has \(session.sections.count) sections")
            for section in session.sections where section.measure != .analyticalWriting {
                let expected = section.ordinal == 1 ? 12 : 15
                check(section.questions.count == expected, "\(section.displayTitle) has \(section.questions.count) questions")
                let minutes = section.durationSeconds / 60
                let expectedMinutes = section.measure == .verbal ? (section.ordinal == 1 ? 18 : 23) : (section.ordinal == 1 ? 21 : 26)
                check(minutes == expectedMinutes, "\(section.displayTitle) lasts \(minutes) minutes")
            }
            for measure in [GREMeasure.verbal, .quantitative] {
                let ordinals = session.sections.filter { $0.measure == measure }.map(\.ordinal)
                check(ordinals == [1, 2], "\(measure.title) sections out of order: \(ordinals)")
            }
            if run < 2, let verbal = session.sections.first(where: { $0.measure == .verbal }) {
                log("  Verbal 1 formats: " + verbal.questions.map { "\($0.format)" }.joined(separator: ","))
            }
            if run < 2, let quant = session.sections.first(where: { $0.measure == .quantitative }) {
                log("  Quant 1 formats: " + quant.questions.map { "\($0.format)\($0.figure == nil ? "" : "*")" }.joined(separator: ","))
            }
            session.completeWithCorrectAnswersForTesting()
            if run < 3 {
                let r = session.result
                log("  Perfect run → V \(r?.verbalScore ?? -1), Q \(r?.quantitativeScore ?? -1), AW \(r?.writingEstimate ?? -1); routes " +
                    (r?.sections.compactMap { $0.difficulty.map { "\($0.title)" } }.joined(separator: "/") ?? ""))
            }
            check(session.result?.verbalScore == 170 && session.result?.quantitativeScore == 170, "Perfect run did not reach 170/170")
        }

        // Score conversion and NS.
        let mid = ScoreEstimator.scaled(measure: .verbal, firstCorrect: 6, firstTotal: 12, secondCorrect: 8, secondTotal: 15, route: .medium)
        let low = ScoreEstimator.scaled(measure: .verbal, firstCorrect: 0, firstTotal: 12, secondCorrect: 0, secondTotal: 15, route: .easy)
        let hardHigh = ScoreEstimator.scaled(measure: .quantitative, firstCorrect: 11, firstTotal: 12, secondCorrect: 13, secondTotal: 15, route: .hard)
        let easyHigh = ScoreEstimator.scaled(measure: .quantitative, firstCorrect: 11, firstTotal: 12, secondCorrect: 13, secondTotal: 15, route: .easy)
        log("Scale: verbal 6/12 + 8/15 medium → \(mid); zero → \(low); quant 11+13 hard → \(hardHigh), easy → \(easyHigh)")
        check(low == 130 && hardHigh > easyHigh && (140...160).contains(mid), "Score conversion out of range")
        let blank = ExamSession(mode: .verbalPractice)
        blank.beginSection(); blank.finishSection(); blank.beginSection(); blank.finishSection()
        check(blank.result?.verbalScore == nil, "Blank verbal test should report NS")
        log("Percentiles: V160 \(ScoreEstimator.percentile(160, measure: .verbal) ?? -1), Q160 \(ScoreEstimator.percentile(160, measure: .quantitative) ?? -1), AW4.0 \(ScoreEstimator.writingPercentile(4.0) ?? -1)")

        // Calculator.
        let calc = CalculatorModel()
        func press(_ keys: [String]) -> String { keys.forEach(calc.input); return calc.display }
        check(press(["2", "+", "3", "×", "4", "="]) == "14", "Order of operations")
        calc.input("C")
        check(press(["(", "2", "+", "3", ")", "×", "4", "="]) == "20", "Parentheses")
        calc.input("C")
        check(press(["1", "÷", "0", "="]) == "ERROR", "Division by zero")
        calc.input("C")
        check(press(["9", "9", "9", "9", "9", "9", "9", "9", "9"]) == "99999999", "8-digit entry limit")
        check(press(["×", "1", "0", "="]) == "ERROR", "Overflow")
        calc.input("C")
        check(press(["2", "√"]) == "1.4142136", "Square root: \(calc.display)")
        calc.input("C")
        check(press(["5", "M+", "C", "MR", "+", "1", "="]) == "6", "Memory")
        log("Calculator: 1÷3 = \(press(["C", "1", "÷", "3", "="]))")

        // Essay scoring.
        let prompt = QuestionBank.writingQuestions.first?.prompt ?? ""
        let blankEssay = IssueEssayScorer.score(essay: "", prompt: prompt)
        let copied = IssueEssayScorer.score(essay: prompt + " " + prompt, prompt: prompt)
        check(blankEssay.score == 0 && copied.score == 0, "Blank or copied essay should score 0")
        log("Writing prompts: \(QuestionBank.writingQuestions.count); with instructions: \(QuestionBank.writingQuestions.filter { $0.instructions != nil }.count)")

        log(failures.isEmpty ? "SELF-TEST PASSED" : "SELF-TEST FAILED:\n" + failures.joined(separator: "\n"))
        exit(failures.isEmpty ? 0 : 1)
    }
}
#endif
