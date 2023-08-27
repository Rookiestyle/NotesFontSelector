using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using KeePass.App.Configuration;
using KeePassLib;
using PluginTools;

namespace NotesFontSelector
{
  internal static class Config
  {
    private const string m_ConfigNotesFont = "NotesFontSelector.NotesFont";

    private static AceCustomConfig m_conf = null;
    static Config()
    {
      m_conf = KeePass.Program.Config.CustomConfig;
    }

    internal static AceFont NotesFont
    {
      get
      {
        var sFont = m_conf.GetString(m_ConfigNotesFont, string.Empty);
        if (string.IsNullOrEmpty(sFont)) return GetDefaultFont();
        using (var sr = new System.IO.StringReader(sFont))
        {
          System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(AceFont));
          try
          {
            var o = xs.Deserialize(new System.IO.StringReader(sFont));
            return (AceFont)o;
          }
          catch (Exception ex)
          {
            PluginDebug.AddError("Error reading config", 0, ex.Message);
          }
        }
        return GetDefaultFont();
      }
      set
      {
        System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(AceFont));
        using (System.IO.StringWriter sw = new System.IO.StringWriter())
        {
          xs.Serialize(sw, value);
          string s = sw.GetStringBuilder().ToString();
          m_conf.SetString(m_ConfigNotesFont, s);
        }
      }
    }

    private static AceFont GetDefaultFont()
    {
      if (KeePass.Program.Config.UI.StandardFont.OverrideUIDefault) return KeePass.Program.Config.UI.StandardFont;
      return new AceFont(KeePass.UI.UISystemFonts.DefaultFont);
    }
  }
}
