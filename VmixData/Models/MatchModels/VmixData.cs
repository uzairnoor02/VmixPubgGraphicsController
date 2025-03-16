
using System.Xml.Serialization;


namespace VmixData.Models.MatchModels
{
    // Root XmlElement for the vmix XML
    [XmlRoot("vmix")]
    public class VmixData
    {
        [XmlElement("version")]
        public string Version { get; set; }

        [XmlElement("edition")]
        public string Edition { get; set; }

        [XmlElement("preset")]
        public string Preset { get; set; }

        [XmlArray("inputs")]
        [XmlArrayItem("input")]
        public Input[] Inputs { get; set; }
    }

    // Represents the input element in the XML
    public class Input
    {
        [XmlAttribute("key")]
        public string Key { get; set; }

        [XmlAttribute("number")]
        public int Number { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("title")]
        public string Title { get; set; }

        [XmlAttribute("shortTitle")]
        public string ShortTitle { get; set; }

        [XmlAttribute("state")]
        public string State { get; set; }

        [XmlAttribute("position")]
        public int Position { get; set; }

        [XmlAttribute("duration")]
        public int Duration { get; set; }

        [XmlAttribute("loop")]
        public string Loop { get; set; }

        [XmlAttribute("selectedIndex")]
        public int SelectedIndex { get; set; }

        [XmlText]
        public string Text { get; set; }

        [XmlElement("text")]
        public TextElement[] TextElements { get; set; }

        [XmlElement("image")]
        public ImageElement[] ImageElements { get; set; }
    }

    // Represents the text element in the input
    public class TextElement
    {
        [XmlAttribute("index")]
        public int Index { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
    public class Overlay
    {
        [XmlAttribute("index")]
        public int Index { get; set; }

        [XmlAttribute("key")]
        public string Key { get; set; }
    }
    // Represents the image element in the input
    public class ImageElement
    {
        [XmlAttribute("index")]
        public int Index { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("Source")]
        public string Source { get; set; }
    }

}
