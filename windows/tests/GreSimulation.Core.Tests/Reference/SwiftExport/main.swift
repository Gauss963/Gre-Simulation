import Foundation

let out: String = CommandLine.arguments[1]
let enc = JSONEncoder()
enc.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
let literal = [QuestionBank.writingQuestion] + QuestionBank.verbalQuestions + QuestionBank.quantitativeQuestions
try! enc.encode(literal).write(to: URL(fileURLWithPath: out + "/SwiftLiteralQuestions.json"))
try! enc.encode(VocabularyBank.curatedWords).write(to: URL(fileURLWithPath: out + "/CuratedVocabulary.json"))

// Reference values for parity tests.
var ref: [String: String] = [:]
MainActor.assumeIsolated {
    let seqs: [[String]] = [
        ["1","÷","3","="], ["2","+","3","×","4","="], ["(","2","+","3",")","×","4","="], ["1","÷","0","="],
        ["9","9","9","9","9","9","9","9","9"], ["9","9","9","9","9","9","9","9","×","1","0","="], ["2","√"],
        ["5","M+","C","MR","+","1","="], ["2","4","6","9","1","3","5","7","÷","2","="], ["1","2","3","4","5","6","7","8","+","0",".","5","="],
        ["±"], ["5","±"], ["5","±","±"], ["3","+","="], ["3","+","×","2","="], ["7","="," 2"], ["2","=","3"], ["2","=","+","3","="],
        ["(","(","2","+","3"], ["(","(","2","+","3","="], [".","5","+",".","2","5","="], ["0",".","1","+","0",".","2","="],
        ["1","0","0","÷","7","="], ["2","÷","3","=","×","3","="], ["1","−","2","="], ["5","CE","3","+","1","="], ["5","+","CE","2","="],
        ["MR"], ["4","M+","M+","MR"], ["4","M+","MC","MR"], ["2","±","√"], ["9","√","+","1","="], ["1","2","3",")"], ["(","5",")","+","1","="],
        ["5","(","2","="], ["0","0","0","1"], [".",".","5"], ["1","2",".","3","4","5","6","7","8","9"], ["1","÷","8","="], ["2","×","(","3","+","4",")","="],
        ["3","+","(","="], ["9","9","9","9","9","9","9","9","M+","M+"], ["1","÷","3","=","+","1","="], ["0",".","0","0","0","0","0","0","1","×","1","="],
        ["1","÷","9","0","0","0","0","0","0","0","="], ["8","0","0","0","0","0","0","0","+","1","9","9","9","9","9","9","9","="], ["5","=","±"], ["1","0","÷","4","="]
    ]
    for (i, s) in seqs.enumerated() {
        let c = CalculatorModel()
        s.forEach(c.input)
        ref["calc.\(i)"] = s.joined(separator: " ") + " => " + c.display + " | " + c.expression + " | " + String(c.memory) + " | " + String(c.hasError) + " | " + c.transferText
    }
}
let fmtValues: [Double] = [1.0/3, 2.0/3, 12345678.5, 0.5, 1.5, 2.5, -0.000000001, 99999999.4, 0.125, 1e-9, 123.456789012, -2.0/3, 3.14159265358979, 0.0000000049, 1234567.45]
MainActor.assumeIsolated { for v in fmtValues { ref["fmt.\(v)"] = CalculatorModel.format(v) } }
for m in [GREMeasure.verbal, .quantitative] {
    for route in [QuestionDifficulty?.none, .easy, .medium, .hard] {
        var row: [String] = []
        for a in 0...12 { for b in 0...15 { row.append(String(ScoreEstimator.scaled(measure: m, firstCorrect: a, firstTotal: 12, secondCorrect: b, secondTotal: 15, route: route))) } }
        ref["scale.\(m.rawValue).\(route?.rawValue ?? "nil")"] = row.joined(separator: ",")
    }
}
for t in ["2.50","5/2","2.51",".5","5.","+5","-3"," 5","5 ","1e3","inf","nan","0x10","1,000","","-","1/0","3/","/3","1/2/3","1.2.3","Infinity","-0","00012","1e","1e+2","e3","٣"] {
    ref["double.\(t)"] = Double(t).map { String($0) } ?? "nil"
}
for (a, b) in [("2.50","5/2"),("2.51","5/2"),("1,000","1000"),(" 3 / 6 ","1/2"),("0.333333333","1/3"),("0.3333333","1/3"),("1E3","1000"),("abc","ABC"),("",""),("-.5","-1/2")] {
    ref["match.\(a)|\(b)"] = String(NumericAnswerMatcher.matches(a, b))
}
let prompt = QuestionBank.writingQuestion.prompt
let essays = [
    "",
    prompt + " " + prompt,
    "I like cats and dogs very much and they are great pets for many people in the world today.",
    Array(repeating: "A clear position should examine evidence, acknowledge reasonable limits, and explain why the conclusion follows. ", count: 35).joined(),
    "I agree that educational institutions should encourage students to question established ideas. For example, students who question ideas learn more deeply.\n\nHowever, critics argue that mastery must come first. Admittedly, some might argue that questioning without knowledge is empty. Nevertheless, I believe students can do both.\n\nFirst, questioning builds understanding. Second, it builds independence. Moreover, history shows that progress depends on challenge. For instance, Galileo questioned established astronomy and transformed science, which demonstrates how institutions benefit.\n\nIn conclusion, schools should encourage questioning established ideas while ensuring students master fundamentals, because thoughtful challenge requires knowledge, and knowledge without challenge stagnates. Therefore educators must balance both goals carefully.",
    String(repeating: "Students should question established ideas in school because institutions shape thinking; for example, science advances when learners challenge assumptions. However, mastery also matters! Consider medicine: doctors must master anatomy before they question it? Thus balance is essential.\n", count: 12)
]
for (i, e) in essays.enumerated() {
    let r = IssueEssayScorer.score(essay: e, prompt: prompt)
    ref["essay.\(i)"] = "\(r.score)|\(r.rubricRating)|\(r.featureRating)|\(r.summary)"
}
let date = Date(timeIntervalSinceReferenceDate: 784_000_000.25)
let sample = ExamResult(id: UUID(uuidString: "E621E1F8-C36C-495A-93FC-0C247A3E6E5F")!, completedAt: date, mode: .verbalPractice, verbalScore: 150, quantitativeScore: nil, writingEstimate: 4.5, verbalCorrect: 10, verbalTotal: 27, quantitativeCorrect: 0, quantitativeTotal: 0, elapsedSeconds: 1234, sections: [SectionSummary(measure: .verbal, ordinal: 1, difficulty: nil, correct: 5, total: 12, answered: 12)], questionReviews: [QuestionReviewRecord(sectionID: "verbal-1", sectionOrdinal: 1, sectionDifficulty: nil, questionNumber: 1, question: QuestionBank.verbalQuestions[0], answer: GREAnswer(selections: ["blank0": ["b0o1"]], numericEntry: "", essayText: ""), isCorrect: true)])
ref["history.sample"] = String(data: try! JSONEncoder().encode([sample]), encoding: .utf8)!
let refEnc = JSONEncoder(); refEnc.outputFormatting = [.prettyPrinted, .sortedKeys]
try! refEnc.encode(ref).write(to: URL(fileURLWithPath: out + "/SwiftReference.json"))
print("done", literal.count, VocabularyBank.curatedWords.count)
