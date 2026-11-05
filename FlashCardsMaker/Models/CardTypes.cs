namespace FlashCardsMaker.Models;

public enum CardType
{
    Verb,
    Adverb,
    Noun,
    IAdjective,
    NaAdjective,
    Expression,
    Particle,
    Counter,
    Kanji,
    Onomatopoeia
}

public enum ExportFormat
{
    PDF,
    PNG,
    JPEG
}

public enum ExportLayout
{
    FrontOnly,
    BackOnly,
    BothInOneFile,
    BothSeparateFiles
}

public enum DuplexFlipEdge
{
    LongEdge,
    ShortEdge
}

public enum VerbKind
{
    Godan,
    Ichidan,
    Irregular
}

public enum VerbTransitivity
{
    Transitive,
    Intransitive
}

public enum AdverbClassification
{
    Positive,
    Neutral,
    Negative
}

public enum NounCategory
{
    Person,
    Place,
    Food,
    Object,
    Animal,
    Time,
    Concept,
    Expression
}

public enum MeaningStyle
{
    Normal,
    Bold,
    Underline,
    BoldUnderline
}

public enum CardStatus
{
    MissingInformation,
    InProgress,
    Complete
}

public enum DeckStatus
{
    Draft,
    InProgress,
    MissingInformation,
    Completed
}

public enum PaperSize
{
    A4,
    Letter
}

public enum PaperOrientation
{
    Portrait,
    Landscape
}

public enum GridLayout
{
    ThreeByThree = 3,
    FourByFour = 4,
    FiveByFive = 5
}



