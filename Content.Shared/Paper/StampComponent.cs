namespace Content.Shared.Paper
{
    [RegisterComponent]
    public class StampComponent : Component
    {
        /// <summary>
        ///     The name that will be stamped to the piece of paper on examine.
        /// </summary>
        [ViewVariables]
        [DataField("stampedName")]
        public string StampedName { get; set; } = "";
        /// <summary>
        ///     Tne sprite state of the stamp to display on the paper from bureacracy.rsi.
        /// </summary>
        [ViewVariables]
        [DataField("stampState")]
        public string StampState { get; set; } = "paper_stamp-generic";
    }
}
