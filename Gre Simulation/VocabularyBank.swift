import Foundation

enum VocabularyBank {
    static let words: [VocabularyWord] = [
        word("abate", "/əˈbeɪt/", "to become less intense", "減弱；緩和", ["subside", "diminish"], "The wind finally abated near dawn."),
        word("abstruse", "/əbˈstruːs/", "difficult to understand", "深奧的", ["esoteric", "arcane"], "The article made an abstruse theory unusually clear."),
        word("acumen", "/ˈækjəmən/", "the ability to make sound judgments", "敏銳；洞察力", ["insight", "shrewdness"], "Her financial acumen helped the small firm survive."),
        word("ambivalent", "/æmˈbɪvələnt/", "having mixed or conflicting feelings", "矛盾的；搖擺不定的", ["uncertain", "conflicted"], "He was ambivalent about leaving a secure position."),
        word("anomaly", "/əˈnɑːməli/", "something that departs from what is expected", "異常；反常事物", ["irregularity", "aberration"], "Researchers first dismissed the result as an anomaly."),
        word("appease", "/əˈpiːz/", "to calm or satisfy a demand", "安撫；平息", ["placate", "pacify"], "The concession failed to appease the critics."),
        word("arbitrary", "/ˈɑːrbətreri/", "based on whim rather than reason", "武斷的；任意的", ["capricious", "random"], "The cutoff date seemed arbitrary."),
        word("assiduous", "/əˈsɪdʒuəs/", "showing persistent care and effort", "勤勉的", ["diligent", "industrious"], "Her assiduous archival work uncovered the missing letters."),
        word("austere", "/ɔːˈstɪr/", "plain, severe, or without comfort", "樸素的；嚴峻的", ["spartan", "stern"], "The building's austere interior has almost no ornament."),
        word("bolster", "/ˈboʊlstər/", "to support or strengthen", "支持；加強", ["reinforce", "fortify"], "New evidence bolstered the original conclusion."),
        word("capricious", "/kəˈprɪʃəs/", "changing suddenly and unpredictably", "反覆無常的", ["fickle", "whimsical"], "A capricious policy makes long-term planning difficult."),
        word("circumspect", "/ˈsɜːrkəmspekt/", "careful to consider risks", "審慎的", ["cautious", "wary"], "The board was circumspect about making promises."),
        word("cogent", "/ˈkoʊdʒənt/", "clear, logical, and convincing", "有說服力的", ["compelling", "persuasive"], "She offered a cogent explanation for the discrepancy."),
        word("conciliatory", "/kənˈsɪliətɔːri/", "intended to restore goodwill", "安撫的；和解的", ["placating", "pacific"], "His conciliatory tone reopened negotiations."),
        word("conjectural", "/kənˈdʒektʃərəl/", "based on incomplete evidence", "推測的", ["speculative", "hypothetical"], "Without records, the date remains conjectural."),
        word("deleterious", "/ˌdeləˈtɪriəs/", "causing harm or damage", "有害的", ["harmful", "detrimental"], "Chronic noise can have deleterious effects on health."),
        word("diffident", "/ˈdɪfɪdənt/", "modest because of low confidence", "缺乏自信的；羞怯的", ["self-effacing", "timid"], "The normally diffident student gave a forceful defense."),
        word("dogmatic", "/dɔːɡˈmætɪk/", "asserting opinions as unquestionably true", "教條的；武斷的", ["doctrinaire", "opinionated"], "A dogmatic approach leaves little room for contrary evidence."),
        word("eclectic", "/ɪˈklektɪk/", "drawn from many different sources", "兼收並蓄的", ["diverse", "wide-ranging"], "The course has an eclectic reading list."),
        word("enervate", "/ˈenərveɪt/", "to weaken or drain energy", "使衰弱", ["debilitate", "exhaust"], "Weeks of uncertainty enervated the team."),
        word("equivocal", "/ɪˈkwɪvəkəl/", "open to more than one interpretation", "模稜兩可的", ["ambiguous", "noncommittal"], "The minister gave an equivocal response."),
        word("erudite", "/ˈerudaɪt/", "having or showing extensive learning", "博學的", ["learned", "scholarly"], "The lecture was erudite without being inaccessible."),
        word("exacerbate", "/ɪɡˈzæsərbeɪt/", "to make a problem worse", "使惡化", ["aggravate", "intensify"], "The delay exacerbated an already tense situation."),
        word("fastidious", "/fæˈstɪdiəs/", "very attentive to accuracy and detail", "一絲不苟的；挑剔的", ["meticulous", "exacting"], "A fastidious editor checked every citation."),
        word("fortuitous", "/fɔːrˈtuːɪtəs/", "happening by chance, often fortunately", "偶然的；幸運巧合的", ["accidental", "serendipitous"], "A fortuitous meeting led to the collaboration."),
        word("hackneyed", "/ˈhæknid/", "overused and no longer original", "陳腐的", ["trite", "banal"], "The speaker relied on hackneyed slogans."),
        word("iconoclast", "/aɪˈkɑːnəklæst/", "a person who challenges cherished beliefs", "反傳統者", ["dissenter", "nonconformist"], "The critic earned a reputation as an iconoclast."),
        word("inchoate", "/ɪnˈkoʊət/", "only partly formed or organized", "初步的；未成形的", ["undeveloped", "nascent"], "The committee turned an inchoate idea into a proposal."),
        word("laconic", "/ləˈkɑːnɪk/", "using very few words", "簡潔寡言的", ["terse", "succinct"], "Her laconic reply ended the discussion."),
        word("lucid", "/ˈluːsɪd/", "clear and easy to understand", "清楚易懂的", ["clear", "intelligible"], "The diagram makes the process lucid."),
        word("mitigate", "/ˈmɪtɪɡeɪt/", "to make less severe", "減輕；緩和", ["alleviate", "moderate"], "Shade trees can mitigate urban heat."),
        word("obdurate", "/ˈɑːbdərət/", "stubbornly refusing to change", "頑固的", ["intransigent", "unyielding"], "The negotiator remained obdurate despite new evidence."),
        word("opaque", "/oʊˈpeɪk/", "difficult to understand or not transparent", "不透明的；晦澀的", ["obscure", "impenetrable"], "The agency's opaque process invited suspicion."),
        word("paradoxical", "/ˌpærəˈdɑːksɪkəl/", "seemingly contradictory but possibly true", "矛盾卻可能真實的", ["contradictory", "counterintuitive"], "It is paradoxical that more choice can reduce satisfaction."),
        word("pragmatic", "/præɡˈmætɪk/", "focused on practical results", "務實的", ["practical", "realistic"], "They adopted a pragmatic compromise."),
        word("provisional", "/prəˈvɪʒənəl/", "temporary or subject to revision", "暫定的", ["tentative", "interim"], "The estimate is provisional until more data arrive."),
        word("reticent", "/ˈretɪsənt/", "unwilling to reveal thoughts or information", "沉默寡言的；有所保留的", ["reserved", "restrained"], "Witnesses were reticent about the details."),
        word("sanguine", "/ˈsæŋɡwɪn/", "optimistic, especially in difficulty", "樂觀的", ["hopeful", "buoyant"], "Analysts were less sanguine about the second quarter."),
        word("scrupulous", "/ˈskruːpjələs/", "very careful to be accurate or ethical", "嚴謹的；正直的", ["conscientious", "meticulous"], "The study used scrupulous controls."),
        word("specious", "/ˈspiːʃəs/", "seemingly plausible but actually false", "似是而非的", ["misleading", "fallacious"], "The argument is attractive but specious."),
        word("tenuous", "/ˈtenjuəs/", "very weak or slight", "薄弱的；牽強的", ["fragile", "insubstantial"], "The conclusion rests on a tenuous analogy."),
        word("trenchant", "/ˈtrentʃənt/", "sharp, effective, and perceptive", "尖銳有力的", ["incisive", "penetrating"], "Her trenchant critique changed the debate."),
        word("ubiquitous", "/juːˈbɪkwɪtəs/", "present or found everywhere", "無所不在的", ["omnipresent", "pervasive"], "Sensors have become ubiquitous in modern devices."),
        word("vacillate", "/ˈvæsəleɪt/", "to alternate between choices or opinions", "猶豫不決", ["waver", "fluctuate"], "The council continued to vacillate between the two plans."),
        word("vindicate", "/ˈvɪndɪkeɪt/", "to show to be right or justified", "證明正確；洗清", ["justify", "exonerate"], "Later evidence vindicated the researcher's method.")
    ]

    private static func word(
        _ word: String,
        _ pronunciation: String,
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
            example: example
        )
    }
}
