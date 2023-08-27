using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using KeePass;
using KeePass.App.Configuration;
using KeePass.Forms;
using KeePass.Plugins;
using KeePass.UI;
using PluginTools;

namespace NotesFontSelector
{
  public sealed class NotesFontSelectorExt : Plugin
  {
    private IPluginHost m_host = null;
    private FontControlGroup m_fcgNotes;

    public override bool Initialize(IPluginHost host)
    {
      Terminate();
      if (host == null) return false;
      m_host = host;

      Tools.DefaultCaption = "Notes Font Selector";

      GlobalWindowManager.WindowAdded += GlobalWindowManager_WindowAdded;
      Tools.OptionsFormShown += Tools_OptionsFormShown;
      Tools.OptionsFormClosed += Tools_OptionsFormClosed;

      return true;
    }

    private void Tools_OptionsFormClosed(object sender, Tools.OptionsFormsEventArgs e)
    {
      if (e.form.DialogResult != DialogResult.OK) return;
      if (m_fcgNotes == null)
      {
        PluginDebug.AddInfo("Error updating plugin options", 0, "m_fcgNotes is null");
        return;
      }
      Config.NotesFont = m_fcgNotes.SelectedFont;
      m_fcgNotes.Dispose();
      m_fcgNotes = null;
      PluginDebug.AddSuccess("Plugin options updated", 0, "Font set to " + Config.NotesFont.ToString(), "OverrideUIDefault: " + Config.NotesFont.OverrideUIDefault.ToString());
    }

    private void Tools_OptionsFormShown(object sender, Tools.OptionsFormsEventArgs e)
    {
      Tools.AddPluginToOverview(GetType().Name);

      AddConfig(e.form as OptionsForm);
    }

    private void AddConfig(OptionsForm of)
    {
      //Get relevant controls
      var m_gbFonts = Tools.GetControl("m_grpFonts", of) as GroupBox;
      if (m_gbFonts == null)
      {
        PluginDebug.AddError("Error adding plugin options", 0, "Could not locate m_grpFonts");
        return;
      }

      var lCB = m_gbFonts.Controls.OfType<CheckBox>().ToList();
      var lB = m_gbFonts.Controls.OfType<Button>().ToList();

      //custom list font & custom password font will stay on top
      //add ours directly underneath
      if (lCB.Count < 2 || lB.Count < 2)
      {
        PluginDebug.AddError("Error adding plugin options", 0,
          "Expected amount of FontControlGroup controls: min 2",
          "Found: " + lCB.Count.ToString() + " / " + lB.Count.ToString());
        return;
      }
      int iAddHeight = lCB[1].Top - lCB[0].Top;

      //Create new controls
      CheckBox m_cbNotesActive;
      Button m_bNotesActive;
      m_fcgNotes = CreateFontControlGroup(lCB, lB, out m_cbNotesActive, out m_bNotesActive);

      //Adjust size and position of controls
      m_cbNotesActive.Top += iAddHeight;
      m_bNotesActive.Top += iAddHeight;
      int iThresholdTop = lB[1].Top;
      m_gbFonts.Height += iAddHeight;
      foreach (Control c in m_gbFonts.Controls)
      {
        if (c.Top > iThresholdTop) c.Top += iAddHeight;
      }

      //Add our controls
      m_gbFonts.Controls.Add(m_cbNotesActive);
      m_gbFonts.Controls.Add(m_bNotesActive);

      PluginDebug.AddSuccess("Plugin options added", 0);
    }

    private FontControlGroup CreateFontControlGroup(List<CheckBox> lCB, List<Button> lB, out CheckBox cbNotesActive, out Button bNotesActive)
    {
      cbNotesActive = new CheckBox();
      cbNotesActive.Name = "NotesFontSelector_cbNotes";
      cbNotesActive.Left = lCB[0].Left;
      cbNotesActive.Top = lCB[1].Top;
      cbNotesActive.AutoSize = lCB[0].AutoSize;
      cbNotesActive.Height = lCB[0].Height;
      cbNotesActive.Width = lCB[0].Width;
      cbNotesActive.Text = KeePass.Resources.KPRes.Notes + ":";

      bNotesActive = new Button();
      bNotesActive.Name = "NotesFontSelector_bNotes";
      bNotesActive.Left = lB[0].Left;
      bNotesActive.Top = lB[1].Top;
      bNotesActive.AutoSize = lB[0].AutoSize;
      bNotesActive.Height = lB[0].Height;
      bNotesActive.Width = lB[0].Width;

      return new FontControlGroup(cbNotesActive, bNotesActive, Config.NotesFont, new AceFont(UISystemFonts.DefaultFont));
    }

    private void GlobalWindowManager_WindowAdded(object sender, GwmWindowEventArgs e)
    {
      if (e.Form is PwEntryForm) e.Form.Shown += OnEntryFormShown;
    }

    private void OnEntryFormShown(object sender, EventArgs e)
    {
      var ef = sender as PwEntryForm;
      if (ef == null) return;
      if (!Config.NotesFont.OverrideUIDefault) return;
      var rtNotes = Tools.GetControl("m_rtNotes", ef);
      if (rtNotes == null)
      {
        PluginDebug.AddError("Error changing notes font", 0, "Could not locate field: m_rtNotes");
        return;
      }
      else if (!(rtNotes is RichTextBox))
      {
        PluginDebug.AddError("Error changing notes font", 0, "Invalid type: Expected RichTextBox - Found " + rtNotes.GetType().FullName);
        return;
      }
      var sOldFont = rtNotes.Font.ToString();
      var sOldAceFont = new AceFont(KeePass.UI.UISystemFonts.DefaultFont).ToString();
      rtNotes.Font = Config.NotesFont.ToFont();
      PluginDebug.AddSuccess("Changed notes font", 0,
        "Old ACE font: " + sOldAceFont,
        "New ACE font:" + Config.NotesFont.ToString(),
        "Old font: " + sOldFont,
        "New: " + rtNotes.Font.ToString());
    }

    public override void Terminate()
    {
      if (m_host == null) return;

      Tools.OptionsFormClosed -= Tools_OptionsFormClosed;
      Tools.OptionsFormShown -= Tools_OptionsFormShown;
      GlobalWindowManager.WindowAdded -= GlobalWindowManager_WindowAdded;

      PluginDebug.SaveOrShow();

      m_host = null;
    }

    public override string UpdateUrl
    {
      get { return "https://raw.githubusercontent.com/rookiestyle/notesfontselector/master/version.info"; }
    }
  }

  internal class FontControlGroup : IDisposable
  {
    private static ConstructorInfo m_ciConstructor = null;
    private static MethodInfo m_miDispose = null;
    private static PropertyInfo m_piSelectedFont = null;
    private static bool m_bInitialized = true;
    private object m_cfgFontGroup = null;
    static FontControlGroup()
    {
      List<string> lMsg = new List<string>();
      var t = typeof(Program).Assembly.GetTypes().FirstOrDefault(x => x.FullName == "KeePass.UI.FontControlGroup");
      if (t == null)
      {
        PluginDebug.AddError("FontControlGroup", 0, "Could not get type: KeePass.UI.FontControlGroup");
        m_bInitialized = false;
        return;
      }
      else { lMsg.Add("Found type: KeePass.UI.FontControlGroup"); }

      m_ciConstructor = t.GetConstructor(new Type[] { typeof(CheckBox), typeof(Button), typeof(AceFont), typeof(AceFont) });
      if (m_ciConstructor == null)
      {
        lMsg.Add("Could not get constructor(CheckBox, Button, AceFont, AceFont)");
        m_bInitialized = false;
      }
      else lMsg.Add("Found constructor(CheckBox, Button, AceFont, AceFont)");

      m_miDispose = t.GetMethod("Dispose");
      if (m_miDispose == null)
      {
        lMsg.Add("Could not get method Dispose");
        m_bInitialized = false;
      }
      else lMsg.Add("Found method Dispose");

      m_piSelectedFont = t.GetProperty("SelectedFont");
      if (m_piSelectedFont == null)
      {
        lMsg.Add("Could not get property SelectedFont");
        m_bInitialized = false;
      }
      else lMsg.Add("Found property SelectedFont");

      PluginDebug.AddInfo("FontControlGroup", 0, lMsg.ToArray());
    }

    public FontControlGroup(CheckBox cb, Button btn, AceFont afCurrent, AceFont afDefault)
    {
      if (!m_bInitialized) return;
      m_cfgFontGroup = m_ciConstructor.Invoke(new object[] { cb, btn, afCurrent, afDefault });
    }

    public void Dispose()
    {
      if (!m_bInitialized) return;
      if (m_cfgFontGroup == null) return;
      lock (m_cfgFontGroup) m_miDispose.Invoke(m_cfgFontGroup, null);
    }

    public AceFont SelectedFont
    {
      get
      {
        if (!m_bInitialized || m_cfgFontGroup == null) return new AceFont(UISystemFonts.DefaultFont);
        return (AceFont)m_piSelectedFont.GetValue(m_cfgFontGroup, null);
      }
    }
  }
}
