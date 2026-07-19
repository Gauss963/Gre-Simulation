import SwiftUI
import Combine

@MainActor
private final class FlashcardProgressStore: ObservableObject {
    @Published private(set) var mastered: Set<String>

    private let defaults: UserDefaults
    private let storageKey = "gre-simulation.flashcards.mastered.v1"

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        mastered = Set(defaults.stringArray(forKey: storageKey) ?? [])
    }

    func setMastered(_ wordID: String, isMastered: Bool) {
        if isMastered {
            mastered.insert(wordID)
        } else {
            mastered.remove(wordID)
        }
        defaults.set(mastered.sorted(), forKey: storageKey)
    }

    func reset() {
        mastered.removeAll()
        defaults.removeObject(forKey: storageKey)
    }
}

private enum FlashcardDeckSize: Int, CaseIterable, Identifiable {
    case twenty = 20
    case fifty = 50
    case oneHundred = 100
    case all = 0

    var id: Int { rawValue }

    var title: String {
        rawValue == 0 ? "All" : "\(rawValue) cards"
    }
}

struct FlashcardStudyView: View {
    let words: [VocabularyWord]

    @StateObject private var progress = FlashcardProgressStore()
    @State private var selectedSource = "All sources"
    @State private var deckSize: FlashcardDeckSize = .twenty
    @State private var unmasteredOnly = true
    @State private var deck: [VocabularyWord] = []
    @State private var currentIndex = 0
    @State private var isRevealed = false
    @State private var masteredThisRound: Set<String> = []
    @State private var missedThisRound: Set<String> = []
    @State private var hasStarted = false
    @State private var confirmReset = false
    @FocusState private var acceptsKeyboardInput: Bool

    private var eligibleWords: [VocabularyWord] {
        words.filter { item in
            let matchesSource = selectedSource == "All sources" || item.sources.contains(selectedSource)
            let matchesProgress = !unmasteredOnly || !progress.mastered.contains(item.id)
            return matchesSource && matchesProgress
        }
    }

    private var currentCard: VocabularyWord? {
        guard currentIndex < deck.count else { return nil }
        return deck[currentIndex]
    }

    private var overallMasteredCount: Int {
        words.lazy.filter { progress.mastered.contains($0.id) }.count
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            controls

            if let currentCard {
                sessionProgress
                flashCard(currentCard)
                ratingControls
            } else if hasStarted, !deck.isEmpty {
                completionSummary
            } else if hasStarted {
                emptyDeck
            }
        }
        .focusable()
        .focused($acceptsKeyboardInput)
        .focusEffectDisabled()
        .onKeyPress(.space) {
            guard currentCard != nil, !isRevealed else { return .ignored }
            revealCard()
            return .handled
        }
        .onKeyPress(.leftArrow) {
            guard currentCard != nil, isRevealed else { return .ignored }
            rateCurrentCard(asMastered: false)
            return .handled
        }
        .onKeyPress(.rightArrow) {
            guard currentCard != nil, isRevealed else { return .ignored }
            rateCurrentCard(asMastered: true)
            return .handled
        }
        .onAppear {
            if !hasStarted {
                startNewDeck()
            }
            restoreKeyboardFocus()
        }
        .confirmationDialog("Reset all flash-card progress?", isPresented: $confirmReset, titleVisibility: .visible) {
            Button("Reset mastery progress", role: .destructive) {
                progress.reset()
                startNewDeck()
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("Every saved mastered word will return to the study pool.")
        }
    }

    private var controls: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Flash-card session")
                        .font(.title2.bold())
                    Text("Mastery is saved on this device.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Menu {
                    Button("Reset mastery progress", role: .destructive) {
                        confirmReset = true
                    }
                } label: {
                    Image(systemName: "ellipsis.circle")
                        .font(.title3)
                }
                .menuStyle(.borderlessButton)
                .accessibilityLabel("Flash-card options")
            }

            ViewThatFits(in: .horizontal) {
                HStack(spacing: 14) { deckSettings }
                VStack(alignment: .leading, spacing: 12) { deckSettings }
            }

            HStack {
                Label("\(eligibleWords.count) available", systemImage: "rectangle.stack")
                Spacer()
                Label("\(overallMasteredCount) mastered", systemImage: "checkmark.seal.fill")
                    .foregroundStyle(GRETheme.teal)
                Button("New shuffled deck") { startNewDeck() }
                    .buttonStyle(SecondaryGREButtonStyle())
            }
            .font(.caption.weight(.semibold))
        }
        .greCard()
    }

    private var deckSettings: some View {
        Group {
            Picker("Source", selection: $selectedSource) {
                Text("All sources").tag("All sources")
                ForEach(VocabularyBank.sourceOptions, id: \.self) { source in
                    Text(source).tag(source)
                }
            }
            .pickerStyle(.menu)

            Picker("Deck size", selection: $deckSize) {
                ForEach(FlashcardDeckSize.allCases) { size in
                    Text(size.title).tag(size)
                }
            }
            .pickerStyle(.menu)

            Toggle("Unmastered only", isOn: $unmasteredOnly)
                .toggleStyle(.switch)
        }
    }

    private var sessionProgress: some View {
        VStack(spacing: 8) {
            HStack {
                Text("Card \(currentIndex + 1) of \(deck.count)")
                    .font(.subheadline.weight(.semibold))
                    .monospacedDigit()
                Spacer()
                Text("\(masteredThisRound.count) known · \(missedThisRound.count) review")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
            }
            ProgressView(value: Double(currentIndex), total: Double(max(deck.count, 1)))
                .tint(GRETheme.teal)
        }
    }

    private func flashCard(_ item: VocabularyWord) -> some View {
        Group {
            if isRevealed {
                cardBack(item)
                    .transition(.opacity.combined(with: .scale(scale: 0.98)))
            } else {
                cardFront(item)
                    .transition(.opacity.combined(with: .scale(scale: 0.98)))
            }
        }
        .padding(32)
        .frame(maxWidth: .infinity, minHeight: 390, alignment: .center)
        .background(GRETheme.surface)
        .clipShape(RoundedRectangle(cornerRadius: 18))
        .overlay(RoundedRectangle(cornerRadius: 18).stroke(GRETheme.border))
        .shadow(color: .black.opacity(0.08), radius: 12, y: 5)
        .contentShape(RoundedRectangle(cornerRadius: 18))
        .onTapGesture {
            if !isRevealed {
                revealCard()
            }
        }
    }

    private func cardFront(_ item: VocabularyWord) -> some View {
        VStack(spacing: 16) {
            if item.isHighFrequency {
                Label("High frequency", systemImage: "flame.fill")
                    .font(.caption.bold())
                    .foregroundStyle(GRETheme.warning)
            }
            Text(item.word)
                .font(.system(size: 48, weight: .bold, design: .serif))
                .multilineTextAlignment(.center)
                .minimumScaleFactor(0.7)
            if !item.pronunciation.isEmpty {
                Text(item.pronunciation)
                    .font(.title3)
                    .foregroundStyle(.secondary)
            }
            Label("Tap the card or press Space to reveal", systemImage: "rectangle.on.rectangle.angled")
                .font(.subheadline)
                .foregroundStyle(GRETheme.blue)
                .padding(.top, 18)
        }
        .frame(maxWidth: .infinity)
        .accessibilityElement(children: .combine)
    }

    private func cardBack(_ item: VocabularyWord) -> some View {
        VStack(alignment: .leading, spacing: 13) {
            HStack(alignment: .firstTextBaseline) {
                Text(item.word)
                    .font(.system(size: 32, weight: .bold, design: .serif))
                if !item.pronunciation.isEmpty {
                    Text(item.pronunciation)
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                Spacer()
                if item.isHighFrequency {
                    Image(systemName: "flame.fill")
                        .foregroundStyle(GRETheme.warning)
                        .accessibilityLabel("High frequency")
                }
            }
            Divider()
            definitionBlock("English definition", item.definition)
            if !item.chinese.isEmpty {
                definitionBlock("Taiwan Traditional Chinese", item.chinese, color: GRETheme.teal)
            }
            if !item.synonyms.isEmpty {
                definitionBlock("Synonyms", item.synonyms.joined(separator: " · "))
            }
            if !item.example.isEmpty {
                Text("Example")
                    .font(.caption.bold())
                    .foregroundStyle(.secondary)
                Text(item.example)
                    .font(.body)
                    .italic()
            }
            if !item.sources.isEmpty {
                Text(item.sources.joined(separator: " · "))
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
                    .padding(.top, 3)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func definitionBlock(_ title: String, _ text: String, color: Color? = nil) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title)
                .font(.caption.bold())
                .foregroundStyle(.secondary)
            Text(text)
                .font(.body.weight(.medium))
                .foregroundStyle(color ?? Color.primary)
        }
    }

    @ViewBuilder
    private var ratingControls: some View {
        if isRevealed {
            HStack(spacing: 14) {
                Button("Study again") { rateCurrentCard(asMastered: false) }
                    .buttonStyle(SecondaryGREButtonStyle())
                Spacer()
                Button("Know it") { rateCurrentCard(asMastered: true) }
                    .buttonStyle(PrimaryGREButtonStyle())
            }
        } else {
            HStack {
                Spacer()
                Button("Reveal answer") { revealCard() }
                    .buttonStyle(PrimaryGREButtonStyle())
            }
        }
    }

    private var completionSummary: some View {
        VStack(spacing: 18) {
            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 48))
                .foregroundStyle(GRETheme.teal)
            Text("Deck complete")
                .font(.largeTitle.bold())
            Text("You knew \(masteredThisRound.count) cards and marked \(missedThisRound.count) for another review.")
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
            HStack(spacing: 14) {
                if !missedThisRound.isEmpty {
                    Button("Review missed cards") { reviewMissedCards() }
                        .buttonStyle(SecondaryGREButtonStyle())
                }
                Button("Start another deck") { startNewDeck() }
                    .buttonStyle(PrimaryGREButtonStyle())
            }
        }
        .frame(maxWidth: .infinity, minHeight: 360)
        .greCard()
    }

    private var emptyDeck: some View {
        ContentUnavailableView {
            Label("No matching cards", systemImage: "checkmark.seal")
        } description: {
            Text(unmasteredOnly
                 ? "Every matching card is mastered. Turn off Unmastered only or reset mastery progress."
                 : "Choose a different source to build a deck.")
        }
        .frame(maxWidth: .infinity, minHeight: 340)
        .greCard()
    }

    private func revealCard() {
        withAnimation(.easeInOut(duration: 0.18)) {
            isRevealed = true
        }
        restoreKeyboardFocus()
    }

    private func rateCurrentCard(asMastered: Bool) {
        guard let currentCard else { return }
        progress.setMastered(currentCard.id, isMastered: asMastered)
        if asMastered {
            masteredThisRound.insert(currentCard.id)
            missedThisRound.remove(currentCard.id)
        } else {
            missedThisRound.insert(currentCard.id)
            masteredThisRound.remove(currentCard.id)
        }
        withAnimation(.easeInOut(duration: 0.18)) {
            currentIndex += 1
            isRevealed = false
        }
        restoreKeyboardFocus()
    }

    private func startNewDeck() {
        let shuffled = eligibleWords.shuffled()
        let limit = deckSize.rawValue == 0 ? shuffled.count : min(deckSize.rawValue, shuffled.count)
        deck = Array(shuffled.prefix(limit))
        currentIndex = 0
        isRevealed = false
        masteredThisRound.removeAll()
        missedThisRound.removeAll()
        hasStarted = true
        restoreKeyboardFocus()
    }

    private func reviewMissedCards() {
        deck = words.filter { missedThisRound.contains($0.id) }.shuffled()
        currentIndex = 0
        isRevealed = false
        masteredThisRound.removeAll()
        missedThisRound.removeAll()
        hasStarted = true
        restoreKeyboardFocus()
    }

    private func restoreKeyboardFocus() {
        DispatchQueue.main.async {
            acceptsKeyboardInput = true
        }
    }
}
