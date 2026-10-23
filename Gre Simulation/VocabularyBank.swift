import Foundation

enum VocabularyBank {
    private static let curatedWords: [VocabularyWord] = [
        word("abate", "/əˈbeɪt/", "v.", "to become or make less intense; to reduce in amount or value; (law) to put an end to", "減弱；緩和；減少；降低；（法律）廢除", ["subside", "diminish"], "The wind finally abated near dawn."),
        word("abstruse", "/əbˈstruːs/", "adj.", "difficult to understand; recondite; obscure", "深奧的；難以理解的；晦澀的", ["esoteric", "arcane"], "The article made an abstruse theory unusually clear."),
        word("acumen", "/ˈækjəmən/", "n.", "the ability to make good judgments and quick decisions; keen insight", "敏銳；精明；洞察力；判斷力", ["insight", "shrewdness"], "Her financial acumen helped the small firm survive."),
        word("ambivalent", "/æmˈbɪvələnt/", "adj.", "having mixed or conflicting feelings about something; uncertain", "（情感或態度）矛盾的；搖擺不定的", ["uncertain", "conflicted"], "He was ambivalent about leaving a secure position."),
        word("anomaly", "/əˈnɑːməli/", "n.", "something that deviates from what is standard or expected; an irregularity", "異常；反常現象；異常事物", ["irregularity", "aberration"], "Researchers first dismissed the result as an anomaly."),
        word("appease", "/əˈpiːz/", "v.", "to pacify or placate by meeting demands; to make concessions to an aggressor; to satisfy or relieve (hunger, thirst)", "安撫；平息；姑息；綏靖；滿足（慾望）", ["placate", "pacify"], "The concession failed to appease the critics."),
        word("arbitrary", "/ˈɑːrbətreri/", "adj.", "based on whim or chance rather than reason; random; using unrestrained power", "任意的；武斷的；專斷的；專橫的", ["capricious", "random"], "The cutoff date seemed arbitrary."),
        word("assiduous", "/əˈsɪdʒuəs/", "adj.", "showing great care, attention, and persistent effort", "勤勉的；刻苦的；專心仔細的；孜孜不倦的", ["diligent", "industrious"], "Her assiduous archival work uncovered the missing letters."),
        word("austere", "/ɔːˈstɪr/", "adj.", "plain and without ornament; severe or strict in manner; without comforts; ascetic", "樸素的；無裝飾的；嚴厲的；嚴峻的；苦行的", ["spartan", "stern"], "The building's austere interior has almost no ornament."),
        word("bolster", "/ˈboʊlstər/", "v./n.", "to support or strengthen; to boost; a long pillow; a support", "v. 支持；加強；鼓舞；n. 長枕；支撐物", ["reinforce", "fortify"], "New evidence bolstered the original conclusion."),
        word("capricious", "/kəˈprɪʃəs/", "adj.", "given to sudden changes of mood or behavior; unpredictable", "反覆無常的；變化莫測的；任性的", ["fickle", "whimsical"], "A capricious policy makes long-term planning difficult."),
        word("circumspect", "/ˈsɜːrkəmspekt/", "adj.", "careful to consider all circumstances and risks; prudent", "謹慎的；周全的", ["cautious", "wary"], "The board was circumspect about making promises."),
        word("cogent", "/ˈkoʊdʒənt/", "adj.", "clear, logical, and convincing; pertinent", "有說服力的；令人信服的；切題的", ["compelling", "persuasive"], "She offered a cogent explanation for the discrepancy."),
        word("conciliatory", "/kənˈsɪliətɔːri/", "adj.", "intended to placate or restore goodwill; willing to make concessions", "安撫的；和解的；調和的", ["placating", "pacific"], "His conciliatory tone reopened negotiations."),
        word("conjectural", "/kənˈdʒektʃərəl/", "adj.", "based on guesswork or incomplete evidence", "推測的；猜測性的", ["speculative", "hypothetical"], "Without records, the date remains conjectural."),
        word("deleterious", "/ˌdeləˈtɪriəs/", "adj.", "causing harm or damage", "有害的；有毒的", ["harmful", "detrimental"], "Chronic noise can have deleterious effects on health."),
        word("diffident", "/ˈdɪfɪdənt/", "adj.", "lacking self-confidence; shy and reserved", "缺乏自信的；羞怯的；拘謹的", ["self-effacing", "timid"], "The normally diffident student gave a forceful defense."),
        word("dogmatic", "/dɔːɡˈmætɪk/", "adj.", "asserting opinions as undeniably true", "武斷的；教條的；自以為是的", ["doctrinaire", "opinionated"], "A dogmatic approach leaves little room for contrary evidence."),
        word("eclectic", "/ɪˈklektɪk/", "adj./n.", "drawing ideas or style from a wide range of sources; a person who selects from various sources", "adj. 兼容並蓄的；多元的；折衷的；n. 折衷主義者", ["diverse", "wide-ranging"], "The course has an eclectic reading list."),
        word("enervate", "/ˈenərveɪt/", "v.", "to drain of energy or vitality; to weaken", "使衰弱；使無力；削弱", ["debilitate", "exhaust"], "Weeks of uncertainty enervated the team."),
        word("equivocal", "/ɪˈkwɪvəkəl/", "adj.", "open to more than one interpretation, often intentionally; of uncertain nature; questionable", "模稜兩可的；含糊的；可疑的", ["ambiguous", "noncommittal"], "The minister gave an equivocal response."),
        word("erudite", "/ˈerudaɪt/", "adj.", "having or showing great knowledge or learning", "博學的；學識淵博的", ["learned", "scholarly"], "The lecture was erudite without being inaccessible."),
        word("exacerbate", "/ɪɡˈzæsərbeɪt/", "v.", "to make a problem, situation, or feeling worse", "使惡化；使加劇", ["aggravate", "intensify"], "The delay exacerbated an already tense situation."),
        word("fastidious", "/fæˈstɪdiəs/", "adj.", "very attentive to accuracy and detail; hard to please; excessively particular; very concerned about cleanliness", "一絲不苟的；挑剔的；過分講究的；潔癖的", ["meticulous", "exacting"], "A fastidious editor checked every citation."),
        word("fortuitous", "/fɔːrˈtuːɪtəs/", "adj.", "happening by chance; lucky or fortunate by coincidence", "偶然的；巧合的；幸運的", ["accidental", "serendipitous"], "A fortuitous meeting led to the collaboration."),
        word("hackneyed", "/ˈhæknid/", "adj.", "overused and lacking originality; trite", "陳腐的；老套的；陳詞濫調的", ["trite", "banal"], "The speaker relied on hackneyed slogans."),
        word("iconoclast", "/aɪˈkɑːnəklæst/", "n.", "a person who attacks cherished beliefs or institutions; a destroyer of religious images", "反傳統者；打破舊習的人；（宗教）破壞聖像者", ["dissenter", "nonconformist"], "The critic earned a reputation as an iconoclast."),
        word("inchoate", "/ɪnˈkoʊət/", "adj.", "just begun; only partly formed or organized; incoherent", "初步的；未成形的；剛開始的；雜亂的", ["undeveloped", "nascent"], "The committee turned an inchoate idea into a proposal."),
        word("laconic", "/ləˈkɑːnɪk/", "adj.", "using very few words; concise to the point of seeming rude or mysterious; terse", "言簡意賅的；簡潔的；寡言的", ["terse", "succinct"], "Her laconic reply ended the discussion."),
        word("lucid", "/ˈluːsɪd/", "adj.", "expressed clearly; easy to understand; mentally clear; bright or translucent", "清晰易懂的；明晰的；神智清醒的；清澈的", ["clear", "intelligible"], "The diagram makes the process lucid."),
        word("mitigate", "/ˈmɪtɪɡeɪt/", "v.", "to make less severe or painful; to alleviate; (law) to lessen the gravity of", "減輕；緩和；（法律）從輕", ["alleviate", "moderate"], "Shade trees can mitigate urban heat."),
        word("obdurate", "/ˈɑːbdərət/", "adj.", "stubbornly refusing to change; resistant to persuasion; unmoved by pity", "頑固的；固執的；冷酷無情的", ["intransigent", "unyielding"], "The negotiator remained obdurate despite new evidence."),
        word("opaque", "/oʊˈpeɪk/", "adj.", "hard to understand; not open or transparent; not allowing light to pass through", "晦澀的；難懂的；不透明的；不透光的", ["obscure", "impenetrable"], "The agency's opaque process invited suspicion."),
        word("paradoxical", "/ˌpærəˈdɑːksɪkəl/", "adj.", "seemingly contradictory but possibly true; self-contradictory", "矛盾的；似非而是的；自相矛盾的", ["contradictory", "counterintuitive"], "It is paradoxical that more choice can reduce satisfaction."),
        word("pragmatic", "/præɡˈmætɪk/", "adj.", "dealing with things sensibly and practically; based on practical results; (linguistics) relating to pragmatics", "務實的；實用的；實用主義的；（語言學）語用的", ["practical", "realistic"], "They adopted a pragmatic compromise."),
        word("provisional", "/prəˈvɪʒənəl/", "adj.", "arranged for the present, possibly to be changed later; temporary", "暫定的；臨時的；暫時的", ["tentative", "interim"], "The estimate is provisional until more data arrive."),
        word("reticent", "/ˈretɪsənt/", "adj.", "not revealing one's thoughts or feelings readily; reserved; inclined to be silent", "沉默寡言的；有所保留的；含蓄的", ["reserved", "restrained"], "Witnesses were reticent about the details."),
        word("sanguine", "/ˈsæŋɡwɪn/", "adj.", "optimistic or confident, especially in a difficult situation; (of complexion) ruddy", "樂觀的；自信的；（臉色）紅潤的", ["hopeful", "buoyant"], "Analysts were less sanguine about the second quarter."),
        word("scrupulous", "/ˈskruːpjələs/", "adj.", "extremely careful and thorough; concerned to avoid doing wrong; principled", "一絲不苟的；嚴謹的；有道德原則的；正直的", ["conscientious", "meticulous"], "The study used scrupulous controls."),
        word("specious", "/ˈspiːʃəs/", "adj.", "superficially plausible but actually wrong; misleadingly attractive", "似是而非的；貌似有理的；虛有其表的", ["misleading", "fallacious"], "The argument is attractive but specious."),
        word("tenuous", "/ˈtenjuəs/", "adj.", "very weak or slight; lacking substance; thin or rarefied", "薄弱的；牽強的；空洞的；稀薄的；纖細的", ["fragile", "insubstantial"], "The conclusion rests on a tenuous analogy."),
        word("trenchant", "/ˈtrentʃənt/", "adj.", "sharply incisive and effective; vigorous; clear-cut", "一針見血的；尖銳有力的；鮮明的", ["incisive", "penetrating"], "Her trenchant critique changed the debate."),
        word("ubiquitous", "/juːˈbɪkwɪtəs/", "adj.", "present, appearing, or found everywhere", "無所不在的；隨處可見的；普遍存在的", ["omnipresent", "pervasive"], "Sensors have become ubiquitous in modern devices."),
        word("vacillate", "/ˈvæsəleɪt/", "v.", "to alternate between opinions or choices; to waver; to sway back and forth", "猶豫不決；搖擺不定；擺動", ["waver", "fluctuate"], "The council continued to vacillate between the two plans."),
        word("vindicate", "/ˈvɪndɪkeɪt/", "v.", "to show to be right or justified; to clear of blame or suspicion; to confirm", "證明正確；為…平反；洗清（罪名）；證實", ["justify", "exonerate"], "Later evidence vindicated the researcher's method.")
    ]

    private static let expandedWords: [VocabularyWord] =
        BundledResource.decode([VocabularyWord].self, named: "ExpandedVocabulary") ?? []

    private static let authorized20260720Words: [VocabularyWord] =
        BundledResource.decode([VocabularyWord].self, named: "Authorized20260720Vocabulary") ?? []

    private static let authorized20260720Part2Words: [VocabularyWord] =
        BundledResource.decode([VocabularyWord].self, named: "Authorized20260720Part2Vocabulary") ?? []

    static let words: [VocabularyWord] = {
        var merged: [String: VocabularyWord] = [:]
        for item in expandedWords + authorized20260720Words + authorized20260720Part2Words + curatedWords {
            let key = item.word.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
            guard !key.isEmpty else { continue }
            if let existing = merged[key] {
                let preferCurated = !item.example.isEmpty && !item.definition.isEmpty
                let preferred = preferCurated ? item : existing
                merged[key] = VocabularyWord(
                    word: preferred.word,
                    pronunciation: preferred.pronunciation.isEmpty ? existing.pronunciation : preferred.pronunciation,
                    definition: preferred.definition.isEmpty ? existing.definition : preferred.definition,
                    chinese: preferred.chinese.isEmpty ? existing.chinese : preferred.chinese,
                    synonyms: Array(Set(existing.synonyms + item.synonyms)).sorted(),
                    example: preferred.example.isEmpty ? existing.example : preferred.example,
                    exampleSource: preferred.exampleSource,
                    exampleSourceURL: preferred.exampleSourceURL,
                    sources: Array(Set(existing.sources + item.sources)).sorted(),
                    isHighFrequency: existing.isHighFrequency || item.isHighFrequency,
                    partOfSpeech: preferred.partOfSpeech ?? existing.partOfSpeech
                )
            } else {
                merged[key] = item
            }
        }
        return merged.values.sorted { $0.word.localizedCaseInsensitiveCompare($1.word) == .orderedAscending }
    }()

    static var sourceOptions: [String] {
        Array(Set(words.flatMap(\.sources))).sorted()
    }

    private static func word(
        _ word: String,
        _ pronunciation: String,
        _ partOfSpeech: String,
        _ definition: String,
        _ chinese: String,
        _ synonyms: [String],
        _ example: String
    ) -> VocabularyWord {
        VocabularyWord(
            word: word,
            pronunciation: pronunciation,
            definition: definition,
            chinese: chinese,
            synonyms: synonyms,
            example: example,
            sources: ["Original app notes"],
            partOfSpeech: partOfSpeech
        )
    }

    static func validate() -> [String] {
        var issues: [String] = []
        if Set(words.map(\.id)).count != words.count { issues.append("Vocabulary bank has duplicate IDs.") }
        let unreviewed = words.filter { ($0.partOfSpeech ?? "").isEmpty || $0.chinese.contains("、") || $0.chinese.contains("/") }
        if !unreviewed.isEmpty {
            issues.append("\(unreviewed.count) words lack a reviewed gloss, e.g. \(unreviewed.prefix(3).map(\.word)).")
        }
        let missingAudio = words.filter { WordPronunciationLibrary.shared.clip(for: $0.word) == nil }
        if !missingAudio.isEmpty {
            issues.append("\(missingAudio.count) words have no bundled pronunciation, e.g. \(missingAudio.prefix(3).map(\.word)).")
        }
        return issues
    }
}
