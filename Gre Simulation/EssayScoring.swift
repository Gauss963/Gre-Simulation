import Foundation
#if os(macOS)
import AppKit
#else
import UIKit
#endif

/// Scores an Analyze an Issue response on the official 0–6 scale.
///
/// ETS averages a trained human rating and an e-rater® rating (a second human resolves large
/// differences) and reports the mean to the nearest half point. This engine mirrors that design with
/// two independent ratings: a rubric reading of position, development, organization, and language
/// control, and a feature model of length, vocabulary, sentence variety, and mechanics. It is a
/// practice estimate, not an ETS score.
enum IssueEssayScorer {
    struct Report: Codable, Equatable {
        let score: Double
        let rubricRating: Int
        let featureRating: Int
        let summary: String
    }

    private static let stopWords: Set<String> = [
        "the", "a", "an", "and", "or", "but", "of", "to", "in", "on", "at", "for", "with", "is", "are", "was",
        "were", "be", "been", "it", "this", "that", "these", "those", "i", "you", "we", "they", "he", "she",
        "as", "by", "from", "so", "if", "not", "do", "does", "did", "have", "has", "had", "will", "would",
        "can", "could", "should", "their", "its", "our", "more", "most", "some", "such", "than", "which", "who"
    ]

    private static let positionMarkers = [
        "i agree", "i disagree", "i believe", "in my view", "i contend", "i would argue", "this essay argues",
        "i maintain", "my position", "to a large extent", "only partially", "largely agree", "largely disagree"
    ]
    private static let concessionMarkers = [
        "admittedly", "granted", "some might argue", "critics", "opponents", "on the other hand", "although",
        "however", "nevertheless", "it is true that", "one could object", "counterargument", "skeptics"
    ]
    private static let exampleMarkers = ["for example", "for instance", "consider", "such as", "in the case of", "to illustrate", "history shows"]
    private static let transitions = [
        "first", "second", "third", "furthermore", "moreover", "in addition", "consequently", "therefore", "thus",
        "as a result", "in contrast", "similarly", "finally", "in conclusion", "ultimately", "because", "since"
    ]

    static func score(essay: String, prompt: String) -> Report {
        let text = essay.trimmingCharacters(in: .whitespacesAndNewlines)
        let words = tokens(text)
        guard words.count >= 15 else {
            return Report(score: 0, rubricRating: 0, featureRating: 0, summary: "Blank or too short to evaluate (0).")
        }
        let promptTokens = tokens(prompt)
        if copiedRatio(words, promptTokens) > 0.6 {
            return Report(score: 0, rubricRating: 0, featureRating: 0, summary: "The response mainly copies the prompt (0).")
        }
        let lowered = text.lowercased()
        let content = words.filter { !stopWords.contains($0) && $0.count > 2 }
        let promptKeys = Set(promptTokens.filter { !stopWords.contains($0) && $0.count > 3 })
        let relevance = Set(content).intersection(promptKeys).count
        if relevance == 0 {
            return Report(score: 0, rubricRating: 0, featureRating: 0, summary: "The response does not address the issue (0).")
        }

        let paragraphs = text.components(separatedBy: "\n").filter { tokens($0).count >= 25 }.count
        let sentences = text.split(whereSeparator: { ".!?".contains($0) }).map { tokens(String($0)).count }.filter { $0 >= 3 }
        let hasPosition = positionMarkers.contains { lowered.contains($0) }
        let concessions = concessionMarkers.filter { lowered.contains($0) }.count
        let examples = exampleMarkers.filter { lowered.contains($0) }.count
        let transitionCount = transitions.filter { lowered.contains($0) }.count
        let diversity = Double(Set(content).count) / Double(max(1, content.count))
        let averageLength = Double(content.reduce(0) { $0 + $1.count }) / Double(max(1, content.count))
        let variety: Double = {
            guard sentences.count > 3 else { return 0.2 }
            let mean = Double(sentences.reduce(0, +)) / Double(sentences.count)
            let sd = sqrt(sentences.map { pow(Double($0) - mean, 2) }.reduce(0, +) / Double(sentences.count))
            return min(1, sd / 7)
        }()
        let misspelled = misspellings(text)
        let errorRate = Double(misspelled) / Double(words.count) * 100

        // Rater 1: rubric reading (position, development, organization, language control).
        var rubric = 1.0
        if words.count >= 120 { rubric += 1 }
        if hasPosition || relevance >= 3 { rubric += 0.5 }
        if paragraphs >= 3 { rubric += 0.5 }
        if paragraphs >= 4 && words.count >= 300 { rubric += 0.5 }
        if examples >= 1 { rubric += 0.5 }
        if examples >= 2 && words.count >= 350 { rubric += 0.5 }
        if concessions >= 1 { rubric += 0.5 }
        if concessions >= 2 && transitionCount >= 4 { rubric += 0.5 }
        if words.count >= 450 && diversity > 0.55 && variety > 0.5 { rubric += 0.5 }
        if errorRate > 4 { rubric -= 1 } else if errorRate > 2 { rubric -= 0.5 }
        if words.count < 150 { rubric = min(rubric, 2) } else if words.count < 250 { rubric = min(rubric, 3) } else if words.count < 350 { rubric = min(rubric, 4) }
        let rubricRating = Int(max(1, min(6, rubric.rounded())))

        // Rater 2: feature model (length, vocabulary, sentence variety, cohesion, mechanics).
        let lengthScore = min(1, Double(words.count) / 550)
        let vocabulary = min(1, 0.6 * min(1, diversity / 0.62) + 0.4 * min(1, (averageLength - 4.2) / 2.2))
        let cohesion = min(1, Double(transitionCount) / 6)
        let mechanics = max(0, 1 - errorRate / 5)
        let organization = min(1, Double(paragraphs) / 5)
        let feature = 1 + 5 * (0.32 * lengthScore + 0.2 * vocabulary + 0.13 * variety + 0.12 * cohesion + 0.13 * mechanics + 0.1 * organization)
        let featureRating = Int(max(1, min(6, feature.rounded())))

        let average = Double(rubricRating + featureRating) / 2
        let score = (average * 2).rounded() / 2
        let level = switch Int(score.rounded(.down)) {
        case 6: "Outstanding: insightful position, compelling development, superior control of language."
        case 5: "Strong: thoughtful position, logically sound development, well organized."
        case 4: "Adequate: clear position with relevant support; generally well controlled language."
        case 3: "Limited: vague or limited development; some lapses in organization or control."
        case 2: "Seriously flawed: unclear position, little relevant support, frequent problems."
        default: "Fundamentally deficient: little evidence of an organized response."
        }
        let summary = "\(level) Ratings \(rubricRating) and \(featureRating) averaged to \(String(format: "%.1f", score)). \(words.count) words · \(paragraphs) paragraphs · \(examples) example signals · \(concessions) counterargument signals · \(misspelled) spelling errors."
        return Report(score: score, rubricRating: rubricRating, featureRating: featureRating, summary: summary)
    }

    private static func tokens(_ text: String) -> [String] {
        text.lowercased().split { !$0.isLetter && $0 != "'" }.map(String.init)
    }

    private static func copiedRatio(_ response: [String], _ prompt: [String]) -> Double {
        func grams(_ words: [String]) -> Set<String> {
            guard words.count >= 4 else { return [] }
            return Set((0...(words.count - 4)).map { words[$0..<($0 + 4)].joined(separator: " ") })
        }
        let r = grams(response)
        return Double(r.intersection(grams(prompt)).count) / Double(max(1, r.count))
    }

    private static func misspellings(_ text: String) -> Int {
        let length = (text as NSString).length
        var count = 0
        var offset = 0
        #if os(macOS)
        let checker = NSSpellChecker.shared
        let tag = NSSpellChecker.uniqueSpellDocumentTag()
        defer { checker.closeSpellDocument(withTag: tag) }
        while offset < length {
            let range = checker.checkSpelling(of: text, startingAt: offset, language: "en_US", wrap: false, inSpellDocumentWithTag: tag, wordCount: nil)
            if range.location == NSNotFound { break }
            count += 1
            offset = range.location + max(1, range.length)
        }
        #else
        let checker = UITextChecker()
        while offset < length {
            let range = checker.rangeOfMisspelledWord(in: text, range: NSRange(location: 0, length: length), startingAt: offset, wrap: false, language: "en_US")
            if range.location == NSNotFound { break }
            count += 1
            offset = range.location + max(1, range.length)
        }
        #endif
        return count
    }
}
