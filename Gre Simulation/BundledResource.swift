import Foundation

enum BundledResource {
    static func decode<T: Decodable>(_ type: T.Type, named name: String) -> T? {
        let candidates = [
            Bundle.main.url(forResource: name, withExtension: "json"),
            Bundle.main.url(forResource: name, withExtension: "json", subdirectory: "Resources")
        ]

        for url in candidates.compactMap({ $0 }) {
            guard let data = try? Data(contentsOf: url) else { continue }
            do {
                return try JSONDecoder().decode(type, from: data)
            } catch {
                assertionFailure("Could not decode \(name).json: \(error)")
            }
        }
        assertionFailure("Missing bundled resource: \(name).json")
        return nil
    }
}
