#!/usr/bin/env swift

import AppKit
import Foundation
import PDFKit
import Vision

struct OCRPage: Codable {
    let page: Int
    let text: String
}

enum OCRFailure: LocalizedError {
    case usage
    case unreadablePDF(String)
    case renderFailed(Int)

    var errorDescription: String? {
        switch self {
        case .usage:
            return "Usage: vision_ocr.swift <input.pdf> [--pages 1,3-5] [--output output.json] [--languages en-US,zh-Hant] [--detect-gaps]"
        case .unreadablePDF(let path):
            return "Unable to open PDF: \(path)"
        case .renderFailed(let page):
            return "Unable to render PDF page \(page)."
        }
    }
}

func selectedPages(specification: String?, pageCount: Int) -> [Int] {
    guard let specification, !specification.isEmpty else {
        return Array(1...pageCount)
    }

    var pages = Set<Int>()
    for component in specification.split(separator: ",") {
        let bounds = component.split(separator: "-", maxSplits: 1).compactMap { Int($0) }
        if bounds.count == 2 {
            for page in min(bounds[0], bounds[1])...max(bounds[0], bounds[1]) where (1...pageCount).contains(page) {
                pages.insert(page)
            }
        } else if let page = bounds.first, (1...pageCount).contains(page) {
            pages.insert(page)
        }
    }
    return pages.sorted()
}

func render(_ page: PDFPage, pageNumber: Int) throws -> CGImage {
    let bounds = page.bounds(for: .mediaBox)
    let longestSide: CGFloat = 2_400
    let scale = longestSide / max(bounds.width, bounds.height)
    let width = max(1, Int((bounds.width * scale).rounded(.up)))
    let height = max(1, Int((bounds.height * scale).rounded(.up)))

    guard let context = CGContext(
        data: nil,
        width: width,
        height: height,
        bitsPerComponent: 8,
        bytesPerRow: 0,
        space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue
    ) else {
        throw OCRFailure.renderFailed(pageNumber)
    }

    context.setFillColor(NSColor.white.cgColor)
    context.fill(CGRect(x: 0, y: 0, width: width, height: height))
    context.saveGState()
    context.scaleBy(x: scale, y: scale)
    page.draw(with: .mediaBox, to: context)
    context.restoreGState()

    guard let image = context.makeImage() else {
        throw OCRFailure.renderFailed(pageNumber)
    }
    return image
}

func recognize(_ image: CGImage, languages: [String], detectsHorizontalGaps: Bool) throws -> String {
    let request = VNRecognizeTextRequest()
    request.recognitionLevel = .accurate
    request.usesLanguageCorrection = true
    request.recognitionLanguages = languages
    request.minimumTextHeight = 0.006

    let handler = VNImageRequestHandler(cgImage: image, options: [:])
    try handler.perform([request])
    let observations = (request.results ?? [])
        .sorted { lhs, rhs in
            let verticalDifference = abs(lhs.boundingBox.midY - rhs.boundingBox.midY)
            if verticalDifference > 0.008 {
                return lhs.boundingBox.midY > rhs.boundingBox.midY
            }
            return lhs.boundingBox.minX < rhs.boundingBox.minX
        }
    guard detectsHorizontalGaps else {
        return observations.compactMap { $0.topCandidates(1).first?.string }.joined(separator: "\n")
    }

    var rows: [[VNRecognizedTextObservation]] = []
    for observation in observations {
        if let last = rows.indices.last,
           let anchor = rows[last].first,
           abs(anchor.boundingBox.midY - observation.boundingBox.midY) <= 0.008 {
            rows[last].append(observation)
        } else {
            rows.append([observation])
        }
    }

    let startsStructuredItem = { (text: String) -> Bool in
        text.range(of: #"^(?:[1-9]|10|[A-I])[\.．]"#, options: .regularExpression) != nil
    }
    var outputLines: [String] = []
    for row in rows {
        let ordered = row.sorted { $0.boundingBox.minX < $1.boundingBox.minX }
        let texts = ordered.compactMap { $0.topCandidates(1).first?.string }
        if texts.filter(startsStructuredItem).count > 1 {
            outputLines.append(contentsOf: texts)
            continue
        }
        var combined = ""
        var previousMaxX: CGFloat?
        for (observation, text) in zip(ordered, texts) {
            if let previousMaxX {
                let gap = observation.boundingBox.minX - previousMaxX
                combined += gap > 0.025 ? " ______ " : " "
            }
            combined += text
            previousMaxX = observation.boundingBox.maxX
        }
        outputLines.append(combined)
    }
    return outputLines.joined(separator: "\n")
}

func run() throws {
    let arguments = Array(CommandLine.arguments.dropFirst())
    guard let input = arguments.first else { throw OCRFailure.usage }

    var pageSpecification: String?
    var output: String?
    var languages = ["en-US", "zh-Hant", "zh-Hans"]
    var detectsHorizontalGaps = false
    var index = 1
    while index < arguments.count {
        switch arguments[index] {
        case "--pages" where index + 1 < arguments.count:
            pageSpecification = arguments[index + 1]
            index += 2
        case "--output" where index + 1 < arguments.count:
            output = arguments[index + 1]
            index += 2
        case "--languages" where index + 1 < arguments.count:
            languages = arguments[index + 1].split(separator: ",").map(String.init)
            index += 2
        case "--detect-gaps":
            detectsHorizontalGaps = true
            index += 1
        default:
            throw OCRFailure.usage
        }
    }

    guard let document = PDFDocument(url: URL(fileURLWithPath: input)) else {
        throw OCRFailure.unreadablePDF(input)
    }

    var results: [OCRPage] = []
    for pageNumber in selectedPages(specification: pageSpecification, pageCount: document.pageCount) {
        try autoreleasepool {
            guard let page = document.page(at: pageNumber - 1) else {
                throw OCRFailure.renderFailed(pageNumber)
            }
            let text = try recognize(
                render(page, pageNumber: pageNumber),
                languages: languages,
                detectsHorizontalGaps: detectsHorizontalGaps
            )
            results.append(OCRPage(page: pageNumber, text: text))
            FileHandle.standardError.write(Data("Recognized page \(pageNumber)/\(document.pageCount)\n".utf8))
        }
    }

    let encoder = JSONEncoder()
    encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
    let data = try encoder.encode(results)
    if let output {
        try data.write(to: URL(fileURLWithPath: output), options: .atomic)
    } else {
        FileHandle.standardOutput.write(data)
        FileHandle.standardOutput.write(Data("\n".utf8))
    }
}

do {
    try run()
} catch {
    FileHandle.standardError.write(Data("\(error.localizedDescription)\n".utf8))
    exit(1)
}
