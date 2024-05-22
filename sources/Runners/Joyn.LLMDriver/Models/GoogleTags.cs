namespace Joyn.LLMDriver.Models
{
    internal class GoogleTags
    {
        internal string TextAnnotations { get; set; }
        internal string Description { get; set; }
        internal string BoundingPoly { get; set; }
        internal string Vertices { get; set; }
        internal string X { get; set; }
        internal string Y { get; set; }

        internal static GoogleTags PascalTags = new GoogleTags()
        {
            TextAnnotations = "TextAnnotations",
            Description = "Description",
            BoundingPoly = "BoundingPoly",
            Vertices = "Vertices",
            X = "X",
            Y = "Y"
        };

        internal static GoogleTags CamelTags = new GoogleTags()
        {
            TextAnnotations = "textAnnotations",
            Description = "description",
            BoundingPoly = "boundingPoly",
            Vertices = "vertices",
            X = "x",
            Y = "y"
        };
    }
}
