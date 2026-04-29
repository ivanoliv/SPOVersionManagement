using System.Collections.Generic;

namespace SPOVersionManagement.Models
{
    public class ExtensionGroup
    {
        public string Name { get; set; }
        public string Color { get; set; }
        public bool Enabled { get; set; } = true;
        public List<string> Extensions { get; set; } = new List<string>();
    }

    public class ExtensionGroupConfig
    {
        public List<ExtensionGroup> Groups { get; set; } = new List<ExtensionGroup>();

        public static ExtensionGroupConfig GetDefaults()
        {
            return new ExtensionGroupConfig
            {
                Groups = new List<ExtensionGroup>
                {
                    new ExtensionGroup
                    {
                        Name = "Office Documents",
                        Color = "#00d4ff",
                        Extensions = new List<string> { ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".vsdx", ".vsd", ".one", ".onetoc2", ".mpp", ".pub", ".pdf", ".xps" }
                    },
                    new ExtensionGroup
                    {
                        Name = "Text & Markup",
                        Color = "#00e676",
                        Extensions = new List<string> { ".txt", ".rtf", ".csv", ".xml", ".html", ".htm", ".md", ".json", ".msg", ".eml", ".odt", ".ods", ".odp" }
                    },
                    new ExtensionGroup
                    {
                        Name = "Videos",
                        Color = "#ff5252",
                        Extensions = new List<string> { ".mp4", ".mov", ".wmv", ".avi", ".mkv", ".m4v", ".mpg", ".mpeg", ".3gp", ".3g2", ".mts", ".m2ts" }
                    },
                    new ExtensionGroup
                    {
                        Name = "Audio",
                        Color = "#ff9800",
                        Extensions = new List<string> { ".mp3", ".wav", ".wma", ".aac", ".flac", ".m4a", ".ogg" }
                    },
                    new ExtensionGroup
                    {
                        Name = "Images",
                        Color = "#ffc107",
                        Extensions = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".svg", ".ico", ".webp" }
                    },
                    new ExtensionGroup
                    {
                        Name = "Design & Graphics",
                        Color = "#7b2cbf",
                        Extensions = new List<string> { ".psd", ".ai", ".indd", ".sketch", ".fig", ".xd" }
                    },
                    new ExtensionGroup
                    {
                        Name = "CAD & Engineering",
                        Color = "#e91e63",
                        Extensions = new List<string> { ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs", ".stl" }
                    },
                    new ExtensionGroup
                    {
                        Name = "Development",
                        Color = "#9e9e9e",
                        Enabled = false,
                        Extensions = new List<string> { ".ps1", ".bat", ".cmd", ".sh", ".vbs", ".py", ".js", ".ts", ".jsx", ".tsx", ".cs", ".java", ".cpp", ".c", ".h", ".hpp", ".rb", ".php", ".go", ".rs", ".swift", ".kt", ".scala", ".css", ".scss", ".sass", ".less", ".sql", ".r", ".ipynb", ".yaml", ".yml", ".toml", ".ini", ".conf" }
                    }
                }
            };
        }
    }
}
