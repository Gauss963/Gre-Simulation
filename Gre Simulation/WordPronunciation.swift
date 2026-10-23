import AVFoundation
import Foundation

/// Pre-rendered neural word clips (Kokoro-82M, Apache-2.0) bundled as one resource pack.
nonisolated final class WordPronunciationLibrary: Sendable {
    static let shared = WordPronunciationLibrary()

    private let pack: Data?
    private let clips: [String: [Int]]

    init(bundle: Bundle = .main) {
        struct Index: Decodable { let clips: [String: [Int]] }
        let indexURL = bundle.url(forResource: "WordPronunciations", withExtension: "json")
        let packURL = bundle.url(forResource: "WordPronunciations", withExtension: "pack")
        let index = indexURL
            .flatMap { try? Data(contentsOf: $0) }
            .flatMap { try? JSONDecoder().decode(Index.self, from: $0) }
        clips = index?.clips ?? [:]
        pack = packURL.flatMap { try? Data(contentsOf: $0, options: .alwaysMapped) }
    }

    func clip(for word: String) -> Data? {
        guard let pack, let range = clips[Self.key(for: word)], range.count == 2,
              range[0] >= 0, range[1] > 0, range[0] + range[1] <= pack.count else { return nil }
        return pack.subdata(in: range[0]..<(range[0] + range[1]))
    }

    static func key(for word: String) -> String {
        word.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
    }
}

enum EnglishVoice {
    /// The highest-quality installed US English voice, skipping novelty voices.
    static let best: AVSpeechSynthesisVoice? = {
        let candidates = AVSpeechSynthesisVoice.speechVoices().filter {
            $0.language == "en-US" && !$0.voiceTraits.contains(.isNoveltyVoice)
        }
        return candidates.max { $0.quality.rawValue < $1.quality.rawValue }
            ?? AVSpeechSynthesisVoice(language: "en-US")
    }()
}
