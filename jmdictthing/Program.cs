using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Timers;
using System.Diagnostics;

namespace jmdictthing
{
    struct Word
    {
        public List<string> kanji;
        public List<string> kana;
        public List<string> definitions;

        public Word(List<string> kanji_in, List<string> kana_in, List<string> definitions_in)
        {
            kanji = kanji_in;
            kana = kana_in;
            definitions = definitions_in;
        }
    }


    public class OpenFileDialogForm : Form
    {

        private Button selectButton;
        private Button selectButton2;
        private OpenFileDialog openFileDialog1;
        private OpenFileDialog openFileDialog2;
        private MemoryStream stream;

        private TextBox textBox1;
        private Dictionary<string, Word> wordDictionary = new Dictionary<string, Word>();

        private static List<string> tokens = new List<string> { "keb", "reb", "gloss", "entry" };

        private List<Word> words = new List<Word>();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new OpenFileDialogForm());

        }

        public OpenFileDialogForm()
        {
            openFileDialog1 = new OpenFileDialog();
            openFileDialog2 = new OpenFileDialog();

            selectButton = new Button
            {
                Size = new Size(100, 20),
                Location = new Point(15, 15),
                Text = "Select xml"
            };
            selectButton2 = new Button
            {
                Size = new Size(100, 20),
                Location = new Point(15, 35),
                Text = "Select csv"

            };
            selectButton.Click += new EventHandler(SelectButton_Click);
            selectButton2.Click += new EventHandler(SelectButton2_Click);
            textBox1 = new TextBox
            {
                Size = new Size(300, 300),
                Location = new Point(15, 50),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            ClientSize = new Size(330, 360);
            Controls.Add(selectButton);
            Controls.Add(selectButton2);
            Controls.Add(textBox1);
        }

        private void SetText(string text)
        {
            textBox1.Text = text;
        }
        private void SelectButton_Click(object sender, EventArgs e)
        {
            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sr = new StreamReader(openFileDialog1.FileName);
                    if(!openFileDialog1.FileName.EndsWith(".xml"))
                    {
                        throw new Exception("File is probably not an xml file");
                    }
                    var xmltext = sr.ReadToEnd();
                    byte[] byteArray = Encoding.UTF8.GetBytes(xmltext);
                    stream = new MemoryStream(byteArray);

                }
                catch(SecurityException ex)
                {
                    MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}");
                }
            }
        }
        private async void SelectButton2_Click(object sender, EventArgs e)
        {
            if(openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sr2 = new StreamReader(openFileDialog2.FileName);
                    if(!openFileDialog2.FileName.EndsWith(".csv"))
                    {
                        throw new Exception("File is probably not an csv file");
                    }
                    var csv = sr2.ReadToEnd();

                    Task<StringBuilder> t = TestReader(stream, csv);
                    StringBuilder resultCsv = await t;
                    SetText(resultCsv.ToString());
                    var originalFilename = openFileDialog2.FileName.Substring(0, openFileDialog2.FileName.Length - 4);
                    File.WriteAllText(originalFilename + "modified.csv", resultCsv.ToString());
                }
                catch(SecurityException ex)
                {
                    MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}");
                }
            }
        }


        async Task<StringBuilder> TestReader(System.IO.Stream stream, string csv)
        {
            StringBuilder resultCsv = new StringBuilder();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            settings.Async = true;
            Debug.WriteLine(DateTime.Now.ToString());

            //Create c# dictionary of the xml dictionary
            using(XmlReader reader = XmlReader.Create(stream, settings))
            {
                string currentToken = "";

                while(!reader.EOF)
                {
                    Word word = new Word(new List<string>(), new List<string>(), new List<string>());

                    await ReadToEntry(reader);
                    await reader.ReadAsync();

                    while(!reader.EOF)
                    {
                        currentToken = await ReadToValue(reader);

                        //next entryreached , all data for current entry already read
                        if(currentToken == "entry")
                            break;

                        switch(currentToken)
                        {
                            case "keb":
                                word.kanji.AddRange(await GetValues(reader, currentToken));
                                break;
                            case "reb":
                                word.kana.AddRange(await GetValues(reader, currentToken));
                                break;
                            case "gloss":
                                word.definitions.AddRange(await GetValues(reader, currentToken));
                                for(int i = 0; i < word.definitions.Count; i++)
                                {
                                    word.definitions[i] = word.definitions[i].Replace(",", "、");
                                }
                                break;
                        }
                    }

                    foreach(var spelling in word.kanji)
                    {
                        if(!wordDictionary.ContainsKey(spelling))
                        {
                            wordDictionary.Add(spelling, word);
                        }
                    }
                    foreach(var spelling in word.kana)
                    {
                        if(!wordDictionary.ContainsKey(spelling))
                        {
                            wordDictionary.Add(spelling, word);
                        }
                    }
                }
            }

            Debug.WriteLine(DateTime.Now.ToString());

            var csvLines = csv.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach(var line in csvLines)
            {
                string[] parts = new string[3];
                parts = line.Split(',');

                if(wordDictionary.ContainsKey(parts[0]))
                {
                    if(parts[1] == "")
                    {
                        var kana = wordDictionary[parts[0]].kana;
                        if(kana.Count > 0)
                        {
                            parts[1] = kana[0];
                        }
                    }
                    if(parts[2] == "")
                    {
                        var definitions = wordDictionary[parts[0]].definitions;
                        var definitionsString = "";

                        if(definitions.Count == 1)
                        {
                            definitionsString = definitions[0];
                        }
                        else
                        {
                            for(int i = 0; i < definitions.Count; i++)
                            {
                                definitionsString += "(" + (i + 1).ToString() + ") " + definitions[i] + " ";
                            }
                        }

                        parts[2] = definitionsString;
                    }
                }
                else { }

                resultCsv.AppendLine(parts[0] + "," + parts[1] + "," + parts[2]);
            }

            return resultCsv;
        }

        private async Task ReadToEntry(XmlReader reader)
        {
            while(reader.Name != "entry")
            {
                await reader.ReadAsync();
            }
        }

        private static async Task<string> ReadToValue(XmlReader reader)
        {
            while(!tokens.Contains(reader.Name) && !reader.EOF)
            {
                await reader.ReadAsync();
            }
            return reader.Name;
        }

        private static async Task<List<string>> GetValues(XmlReader reader, string token)
        {
            var definitions = new List<string>();

            while(reader.Name == token && !reader.EOF)
            {
                await reader.ReadAsync();
                if(reader.NodeType == XmlNodeType.Text)
                {
                    definitions.Add(reader.Value);
                }
                await reader.ReadAsync();
            }
            return definitions;
        }
    }
}
