namespace BrumaPx.Data
{
    /// <summary>
    /// State of a wound in one body zone. A blow opens it; a salve cleans it; a bandage seals it. A wound that is
    /// open lets the Bruma in fast, a clean one a little, a sealed one not at all (until a blow opens it again).
    /// </summary>
    public enum WoundState { None, Open, Cleaned, Bandaged }
}
