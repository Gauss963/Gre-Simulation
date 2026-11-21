import Foundation

/// A meaning group of words that often appear as Text Completion and Sentence Equivalence answer
/// choices: one or two poles of near-synonyms, and when there are two, the poles are opposites.
struct AnswerChoiceGroup: Codable, Identifiable, Hashable {
    struct Pole: Codable, Hashable {
        let label: String
        let words: [String]
    }

    let id: String
    let title: String
    let poles: [Pole]
}

/// `Resources/AnswerChoiceWords.json`: the meaning groups and how many Text Completion and Sentence
/// Equivalence options in the question bank use each word (inflected forms counted with the base word).
struct AnswerChoiceWordSet: Codable {
    let groups: [AnswerChoiceGroup]
    let counts: [String: Int]
}

/// Parts of speech the Answer-choice words deck can be narrowed to.
enum AnswerChoicePartOfSpeech: String, CaseIterable, Identifiable {
    case all
    case adjectives
    case verbs
    case nouns

    var id: String { rawValue }

    var title: String {
        switch self {
        case .all: "All parts of speech"
        case .adjectives: "Adjectives"
        case .verbs: "Verbs"
        case .nouns: "Nouns"
        }
    }

    private var tag: String? {
        switch self {
        case .all: nil
        case .adjectives: "adj"
        case .verbs: "v"
        case .nouns: "n"
        }
    }

    /// `adj./n.` matches both Adjectives and Nouns.
    func matches(_ word: VocabularyWord) -> Bool {
        guard let tag else { return true }
        let tags = (word.partOfSpeech ?? "").split(separator: "/").map { $0.trimmingCharacters(in: CharacterSet(charactersIn: ". ")) }
        return tags.contains(tag)
    }
}

/// How an Answer-choice words deck is ordered.
enum AnswerChoiceOrder: String, CaseIterable, Identifiable {
    case groups
    case frequency
    case shuffled

    var id: String { rawValue }

    var title: String {
        switch self {
        case .groups: "Meaning groups together"
        case .frequency: "Most frequent first"
        case .shuffled: "Shuffled"
        }
    }
}

enum AnswerChoiceWords {
    static let set: AnswerChoiceWordSet =
        BundledResource.decode(AnswerChoiceWordSet.self, named: "AnswerChoiceWords") ?? AnswerChoiceWordSet(groups: [], counts: [:])

    private static let bankByKey: [String: VocabularyWord] =
        Dictionary(VocabularyBank.words.map { ($0.word.lowercased(), $0) }, uniquingKeysWith: { first, _ in first })

    private static let placement: [String: (group: AnswerChoiceGroup, pole: Int)] = {
        var result: [String: (group: AnswerChoiceGroup, pole: Int)] = [:]
        for group in set.groups {
            for (index, pole) in group.poles.enumerated() {
                for word in pole.words where result[word.lowercased()] == nil {
                    result[word.lowercased()] = (group, index)
                }
            }
        }
        return result
    }()

    /// Every grouped word, in group order (pole by pole), as its vocabulary-bank entry.
    static let words: [VocabularyWord] = set.groups.flatMap { group in
        group.poles.flatMap { pole in pole.words.compactMap { bankByKey[$0.lowercased()] } }
    }

    static func entry(for word: String) -> VocabularyWord? { bankByKey[word.lowercased()] }

    static func count(for word: VocabularyWord) -> Int { set.counts[word.word.lowercased()] ?? 0 }

    static func group(for word: VocabularyWord) -> (group: AnswerChoiceGroup, pole: Int)? {
        placement[word.word.lowercased()]
    }

    /// The first sense of a gloss, for the short list of group members on the card back.
    static func shortGloss(_ word: VocabularyWord) -> String {
        var gloss = word.chinese
        if let range = gloss.range(of: #"^(adj|adv|v|n)\.\s*"#, options: .regularExpression) {
            gloss.removeSubrange(range)
        }
        return gloss.components(separatedBy: "；").first?.trimmingCharacters(in: .whitespaces) ?? gloss
    }

    /// Orders an eligible deck: whole groups in shuffled order (each group pole by pole, so the
    /// opposites come back to back), most frequent first, or fully shuffled.
    static func ordered(_ eligible: [VocabularyWord], by order: AnswerChoiceOrder) -> [VocabularyWord] {
        switch order {
        case .shuffled:
            return eligible.shuffled()
        case .frequency:
            return eligible.sorted {
                let (a, b) = (count(for: $0), count(for: $1))
                return a == b ? $0.word.localizedCaseInsensitiveCompare($1.word) == .orderedAscending : a > b
            }
        case .groups:
            let byGroup = Dictionary(grouping: eligible) { group(for: $0)?.group.id ?? "" }
            let position = Dictionary(uniqueKeysWithValues: words.enumerated().map { ($1.id, $0) })
            return byGroup.keys.shuffled().flatMap { key in
                (byGroup[key] ?? []).sorted { (position[$0.id] ?? 0) < (position[$1.id] ?? 0) }
            }
        }
    }

    static func validate() -> [String] {
        var issues: [String] = []
        if set.groups.isEmpty { issues.append("Answer-choice word groups did not load.") }
        if Set(set.groups.map(\.id)).count != set.groups.count { issues.append("Answer-choice group IDs must be unique.") }
        let listed = set.groups.flatMap { $0.poles.flatMap(\.words) }.map { $0.lowercased() }
        if Set(listed).count != listed.count { issues.append("An answer-choice word is in more than one group.") }
        let missing = listed.filter { bankByKey[$0] == nil }
        if !missing.isEmpty { issues.append("\(missing.count) answer-choice words are not in the vocabulary bank, e.g. \(missing.prefix(3)).") }
        let uncounted = listed.filter { set.counts[$0] == nil }
        if !uncounted.isEmpty { issues.append("\(uncounted.count) answer-choice words have no count, e.g. \(uncounted.prefix(3)).") }
        for group in set.groups where !(1...2).contains(group.poles.count) || group.poles.contains(where: { $0.words.isEmpty }) {
            issues.append("Answer-choice group \(group.id) needs one or two non-empty poles.")
        }
        return issues
    }
}
