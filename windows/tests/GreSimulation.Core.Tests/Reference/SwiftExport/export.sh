#!/bin/zsh
# Regenerates the Swift-derived data used by the Windows port, from the Mac app's own Swift code:
#   src/GreSimulation.Core/Data/SwiftLiteralQuestions.json  (QuestionBank.writingQuestion + Verbal/Quant literals)
#   src/GreSimulation.Core/Data/CuratedVocabulary.json      (VocabularyBank.curatedWords)
#   tests/GreSimulation.Core.Tests/Reference/SwiftReference.json (calculator, scoring, essay, JSON parity values)
# Run on a Mac with Xcode after changing those Swift files. The Mac sources are only copied, never modified.
set -euo pipefail
here=${0:A:h}
repo=${here:h:h:h:h:h}
mac="$repo/Gre Simulation"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
cp "$mac"/{Models,QuestionBank,VerbalQuestionBank,QuantitativeQuestionBank,BundledResource,WordPronunciation,ExamSession,FormBuilder,Theme,CalculatorView}.swift "$work/"
# curatedWords is private in the app; the export needs to read it.
sed 's/private static let curatedWords/static let curatedWords/' "$mac/VocabularyBank.swift" > "$work/VocabularyBank.swift"
# Reference essay scores are computed without the macOS spell checker (the Windows default).
sed 's/let misspelled = misspellings(text)/let misspelled = 0/' "$mac/EssayScoring.swift" > "$work/EssayScoring.swift"
cp "$here/main.swift" "$work/"
mkdir -p "$work/out"
DEVELOPER_DIR=${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer} xcrun swiftc -Onone -swift-version 5 -o "$work/export" "$work"/*.swift
"$work/export" "$work/out"
cp "$work/out/SwiftLiteralQuestions.json" "$work/out/CuratedVocabulary.json" "$repo/windows/src/GreSimulation.Core/Data/"
cp "$work/out/SwiftReference.json" "$here/../"
echo "Exported Swift data."
