using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using Photon.Deterministic;
using System.Text;

public abstract class QuantumFrameDifferGUI {

  [Serializable]
  private class StateEntry {
    public string RunnerId;
    public int ActorId;
    public int FrameNumber;
    public string CompressedFrameDump;
    [NonSerialized]
    public string FrameDump;
  }

  internal class FrameData {
    public String String;
    public Int32 Diffs;
    public List<string> Lines = new List<string>();
    public Boolean Initialized;
  }

  public int ReferenceActorId = 0;

  [Serializable]
  public class FrameDifferState : ISerializationCallbackReceiver {
    [SerializeField]
    private List<StateEntry> Entries = new List<StateEntry>();

    private Dictionary<string, Dictionary<int, Dictionary<int, FrameData>>> _byRunner = new Dictionary<string, Dictionary<int, Dictionary<int, FrameData>>>();

    public void Clear() {
      Entries.Clear();
      _byRunner.Clear();
    }

    public void AddEntry(string runnerId, int actorId, int frameNumber, string frameDump) {
      var entry = new StateEntry() {
        RunnerId = runnerId,
        ActorId = actorId,
        FrameDump = frameDump,
        FrameNumber = frameNumber
      };
      Entries.Add(entry);
      OnEntryAdded(entry);
    }

    public void OnAfterDeserialize() {
      _byRunner.Clear();
      foreach (var entry in Entries) {
        if (!string.IsNullOrEmpty(entry.CompressedFrameDump)) {
          entry.FrameDump = ByteUtils.GZipDecompressString(ByteUtils.Base64Decode(entry.CompressedFrameDump), Encoding.UTF8);
        }
        OnEntryAdded(entry);
      }
    }

    public void OnBeforeSerialize() {
      foreach (var entry in Entries) {
        if (string.IsNullOrEmpty(entry.CompressedFrameDump)) {
          entry.CompressedFrameDump = ByteUtils.Base64Encode(ByteUtils.GZipCompressString(entry.FrameDump, Encoding.UTF8));
        }
      }
    }

    private void OnEntryAdded(StateEntry entry) {
      if (!_byRunner.TryGetValue(entry.RunnerId, out var byFrame)) {
        _byRunner.Add(entry.RunnerId, byFrame = new Dictionary<int, Dictionary<int, FrameData>>());
      }
      if (!byFrame.TryGetValue(entry.FrameNumber, out var byActor)) {
        byFrame.Add(entry.FrameNumber, byActor = new Dictionary<int, FrameData>());
      }
      if (!byActor.ContainsKey(entry.ActorId)) {
        byActor.Add(entry.ActorId, new FrameData() {
          String = entry.FrameDump
        });
      }
    }

    public IEnumerable<string> RunnerIds => _byRunner.Keys;

    internal Dictionary<int, FrameData> GetFirstFrameDiff(string runnerId, out int frameNumber) {
      if (_byRunner.TryGetValue(runnerId, out var byFrame)) {
        frameNumber = byFrame.Keys.First();
        return byFrame[frameNumber];
      }
      frameNumber = 0;
      return null;
    }
  }

  String _search = "";
  String _gameId;
  Int32 _scrollOffset;
  protected Boolean _hidden;

  const float HeaderHeight = 28.0f;

  protected QuantumFrameDifferGUI(FrameDifferState state) {
    State = state;
  }

  public FrameDifferState State { get; set; }


  public virtual Boolean IsEditor {
    get { return false; }
  }

  public virtual Int32 TextLineHeight {
    get { return 16; }
  }

  public virtual GUIStyle DiffBackground {
    get { return GUI.skin.box; }
  }

  public virtual GUIStyle DiffHeader {
    get { return GUI.skin.box; }
  }

  public virtual GUIStyle DiffHeaderError {
    get { return GUI.skin.box; }
  }

  public virtual GUIStyle DiffLineOverlay {
    get { return GUI.skin.textField; }
  }

  public virtual GUIStyle MiniButton {
    get { return GUI.skin.button; }
  }

  public virtual GUIStyle TextLabel {
    get { return GUI.skin.label; }
  }

  public virtual GUIStyle BoldLabel {
    get { return GUI.skin.label; }
  }

  public virtual GUIStyle MiniButtonLeft {
    get { return GUI.skin.button; }
  }

  public virtual GUIStyle MiniButtonRight {
    get { return GUI.skin.button; }
  }

  public abstract Rect Position {
    get;
  }

  public virtual float ScrollWidth => 16.0f;

  private StringComparer Comparer => StringComparer.InvariantCulture;

  public virtual void Repaint() {

  }

  public abstract void DrawHeader();


  public void Show() {
    _hidden = false;
  }

  public void OnGUI() {
    if (Event.current.type == EventType.ScrollWheel) {
      _scrollOffset += (int)(Event.current.delta.y * 1);
      Repaint();
    }

    DrawSelection();

    if (State?.RunnerIds.Any() != true) {
      DrawNoDumps();
      return;
    }

    DrawDiff();
  }

  void DrawNoDumps() {
    GUILayout.BeginVertical();
    GUILayout.FlexibleSpace();
    GUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    GUILayout.Label("No currently active diffs");
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();
    GUILayout.FlexibleSpace();
    GUILayout.EndVertical();
  }

  void DrawSelection() {
    GUILayout.Space(5);
    using (new GUILayout.HorizontalScope()) {
      try {
        DrawHeader();

        if (GUILayout.Button("Clear", MiniButton, GUILayout.Height(16))) {
          State.Clear();
        }

        if (_hidden) {
          return;
        }

        GUILayout.Space(16);

        GUIStyle styleSelectedButton;
        styleSelectedButton = new GUIStyle(MiniButton);
        styleSelectedButton.normal = styleSelectedButton.active;

        // select the first game if not selected
        if (_gameId == null || !State.RunnerIds.Contains(_gameId)) {
          _gameId = State.RunnerIds.FirstOrDefault();
        }

        foreach (var gameId in State.RunnerIds) {
          if (GUILayout.Button(gameId, gameId == _gameId ? styleSelectedButton : MiniButton, GUILayout.Height(16))) {
            _gameId = gameId;
          }
        }

      } finally {
        GUILayout.FlexibleSpace();
      }
    }

    Rect topBarRect;
    topBarRect = CalculateTopBarRect();
    topBarRect.x = (topBarRect.width - 200) - 3;
    topBarRect.width = 200;
    topBarRect.height = 18;
    topBarRect.y += 3;

    var currentSearch = _search;

    _search = GUI.TextField(topBarRect, _search ?? "");

    if (currentSearch != _search) {
      Search(GetSelectedFrameData().Values.FirstOrDefault(), 0, +1);
    }

    Rect prevButtonRect;
    prevButtonRect = topBarRect;
    prevButtonRect.height = 16;
    prevButtonRect.width = 50;
    prevButtonRect.x -= 102;
    prevButtonRect.y += 1;

    if (GUI.Button(prevButtonRect, "Prev", MiniButtonLeft)) {
      Search(GetSelectedFrameData().Values.FirstOrDefault(), _scrollOffset - 1, -1);
    }

    Rect nextButtonRect;
    nextButtonRect = prevButtonRect;
    nextButtonRect.x += 50;

    if (GUI.Button(nextButtonRect, "Next", MiniButtonRight)) {
      Search(GetSelectedFrameData().Values.FirstOrDefault(), _scrollOffset + 1, +1);
    }
  }

  void DrawDiff() {
    if (_hidden) {
      return;
    }

    var frameData = GetSelectedFrameData();
    if (frameData == null) {
      return;
    }

    // set of lines that are currently being drawn and have diffs
    List<Rect> modified = new List<Rect>();
    List<Rect> added = new List<Rect>();
    List<Rect> removed = new List<Rect>();

    // main background rect
    Rect mainRect;
    mainRect = CalculateMainRect(frameData.Count);

    var scrollBarRect = Position;
    scrollBarRect.y = 25;
    scrollBarRect.height -= 25;
    scrollBarRect.x = scrollBarRect.width - ScrollWidth;
    scrollBarRect.width = ScrollWidth;

    // header rect for drawing title/prev/next background
    Rect headerRect;
    headerRect = Position;
    headerRect.x = 4;
    headerRect.y = HeaderHeight;
    headerRect.width -= ScrollWidth;
    headerRect.width /= frameData.Count;
    headerRect.width -= 8;
    headerRect.height = 23;

    if (!frameData.TryGetValue(ReferenceActorId, out var baseFrame)) {
      ReferenceActorId = frameData.Keys.OrderBy(x => x).First();
      baseFrame = frameData[ReferenceActorId];
    }

    var visibleRows = Mathf.FloorToInt((mainRect.height - HeaderHeight) / TextLineHeight);
    var maxScroll = Math.Max(0, baseFrame.Lines.Count - visibleRows);
    
    if (visibleRows > maxScroll) {
      _scrollOffset = 0;
      GUI.VerticalScrollbar(scrollBarRect, 0, 1, 0, 1);
    } else { 
      _scrollOffset = Mathf.RoundToInt(GUI.VerticalScrollbar(scrollBarRect, _scrollOffset, visibleRows, 0, baseFrame.Lines.Count));
    }

    foreach (var kvp in frameData.OrderBy(x => x.Key)) {

      GUI.Box(mainRect, "", DiffBackground);

      // draw lines
      for (Int32 i = 0; i < 100; ++i) {
        var lineIndex = _scrollOffset + i;
        if (lineIndex < kvp.Value.Lines.Count) {
          var line = kvp.Value.Lines[lineIndex];
          var baseLine = baseFrame.Lines[lineIndex];

          var r = CalculateLineRect(i, mainRect);

          // label
          if (line == null) {
            if (baseLine != null) {
              removed.Add(r);
            }
          } else {
            GUI.Label(r, line, TextLabel);
            if (baseLine == null) {
              added.Add(r);
            } else if (!Comparer.Equals(line, baseFrame.Lines[lineIndex])) {
              modified.Add(r);
            }
          }
        }
      }

      // draw header background
      if (kvp.Value.Diffs > 0) {
        GUI.Box(headerRect, "", DiffHeaderError);
      } else {
        GUI.Box(headerRect, "", DiffHeader);
      }

      // titel label 
      Rect titleRect;
      titleRect = headerRect;
      titleRect.width = headerRect.width / 2;
      titleRect.y += 3;
      titleRect.x += 3;

      var title = String.Format("Client {0}, Diffs: {1}", kvp.Key, kvp.Value.Diffs);
      GUI.Label(titleRect, title, BoldLabel);

      // disable group for prev/next buttons
      GUI.enabled = kvp.Value.Diffs > 0;

      // base button
      Rect setAsReferenceButton = titleRect;
      setAsReferenceButton.height = 15;
      setAsReferenceButton.width = 60;
      setAsReferenceButton.x = headerRect.x + (headerRect.width - 195);

      GUI.enabled = (ReferenceActorId != kvp.Key);
      if (GUI.Button(setAsReferenceButton, "Reference", MiniButton)) {
        ReferenceActorId = kvp.Key;
        Diff(frameData);
        GUIUtility.ExitGUI();
      }
      GUI.enabled = true;

      // next button
      Rect nextButtonRect;
      nextButtonRect = setAsReferenceButton;
      nextButtonRect.x += 65;

      if (GUI.Button(nextButtonRect, "Next Diff", MiniButton)) {
        SearchDiff(kvp.Value, baseFrame, _scrollOffset + 1, +1);
      }

      // prev button
      Rect prevButtonRect;
      prevButtonRect = nextButtonRect;
      prevButtonRect.x += 65;

      if (GUI.Button(prevButtonRect, "Prev Diff", MiniButton)) {
        SearchDiff(kvp.Value, baseFrame, _scrollOffset - 1, -1);
      }

      GUI.enabled = true;

      mainRect.x += mainRect.width;
      headerRect.x += mainRect.width;
    }

    mainRect = CalculateMainRect(frameData.Count);


    // store gui color
    var c = GUI.color;

    // override with semi red & draw diffing lines overlays
    {
      GUI.color = new Color(1, 0.6f, 0, 0.25f); 
      foreach (var diff in modified) {
        GUI.Box(diff, "", DiffLineOverlay);
      }
    }
    {
      GUI.color = new Color(0, 1, 0, 0.25f);
      foreach (var diff in added) {
        GUI.Box(diff, "", DiffLineOverlay);
      }
    }
    {
      GUI.color = new Color(1, 0, 0, 0.25f);
      foreach (var diff in removed) {
        GUI.Box(diff, "", DiffLineOverlay);
      }
    }

    // restore gui color
    GUI.color = c;
  }

  Rect CalculateLineRect(Int32 line, Rect mainRect) {
    Rect r = mainRect;
    r.height = TextLineHeight;
    r.y += HeaderHeight;
    r.y += line * TextLineHeight;
    r.x += 4;
    r.width -= 8;

    return r;
  }

  Rect CalculateTopBarRect() {
    Rect mainRect;
    mainRect = Position;
    mainRect.x = 0;
    mainRect.y = 0;
    mainRect.height = 25;
    return mainRect;
  }

  Rect CalculateMainRect(Int32 frameDataCount) {
    Rect mainRect;
    mainRect = Position;
    mainRect.x = 0;
    mainRect.y = 25;
    mainRect.width -= ScrollWidth;
    mainRect.width /= frameDataCount;
    mainRect.height -= mainRect.y;
    return mainRect;
  }

  void SearchDiff(FrameData frameData, FrameData baseFrame, Int32 startIndex, Int32 searchDirection) {
    for (Int32 i = startIndex; i >= 0 && i < frameData.Lines.Count; i += searchDirection) {
      if (!Comparer.Equals(baseFrame.Lines[i], frameData.Lines[i])) {
        _scrollOffset = i;
        break;
      }
    }
  }

  void Search(FrameData frameData, Int32 startIndex, Int32 searchDirection) {
    var term = _search ?? "";
    if (term.Length > 0) {
      for (Int32 i = startIndex; i >= 0 && i < frameData.Lines.Count; i += searchDirection) {
        if (frameData.Lines[i].Contains(term)) {
          _scrollOffset = i;
          break;
        }
      }
    }
  }


  Dictionary<Int32, FrameData> GetSelectedFrameData() {

    var frames = State.GetFirstFrameDiff(_gameId, out int frameNumber);
    if (frames == null)
      return null;

    foreach (var frame in frames.Values) {
      if (!frame.Initialized) {
        Diff(frames);
        break;
      }
    }

    return frames;
  }

  void Diff(Dictionary<Int32, FrameData> frames) {

    foreach (var frame in frames.Values) {
      frame.Initialized = false;
      frame.Diffs = 0;
      frame.Lines.Clear();
    }

    // diff all lines
    if (!frames.TryGetValue(ReferenceActorId, out var baseFrame)) {
      ReferenceActorId = frames.Keys.OrderBy(x => x).First();
      baseFrame = frames[ReferenceActorId];
    }

    var otherFrames = frames.Where(x => x.Key != ReferenceActorId).OrderBy(x => x.Key).Select(x => x.Value).ToArray();

    var splits = new[] { "\r\n", "\r", "\n" };
    var baseLines = baseFrame.String.Split(splits, StringSplitOptions.None);

    var diffs = new List<ValueTuple<string, string>>[otherFrames.Length];

    // compute lcs
    Parallel.For(0, otherFrames.Length, () => new LongestCommonSequence(), (frameIndex, state, lcs) => {
      var frameLines = otherFrames[frameIndex].String.Split(splits, StringSplitOptions.None);
      otherFrames[frameIndex].Diffs = 0;

      var chunks = new List<LongestCommonSequence.DiffChunk>();
      lcs.Diff(baseLines, frameLines, Comparer, chunks);

      var diff = new List<ValueTuple<string, string>>();

      int baseLineIndex = 0;
      int frameLineIndex = 0;

      foreach (var chunk in chunks) {

        int sameCount = chunk.StartA - baseLineIndex;
        Debug.Assert(chunk.StartB - frameLineIndex == sameCount);

        int modifiedCount = Mathf.Min(chunk.AddedA, chunk.AddedB);
        otherFrames[frameIndex].Diffs += Mathf.Max(chunk.AddedA, chunk.AddedB);

        for (int i = 0; i < sameCount + modifiedCount; ++i) {
          diff.Add((baseLines[baseLineIndex++], frameLines[frameLineIndex++]));
        }

        for (int i = 0; i < chunk.AddedA - modifiedCount; ++i) {
          diff.Add((baseLines[baseLineIndex++], default));
        }

        for (int i = 0; i < chunk.AddedB - modifiedCount; ++i) {
          diff.Add((default, frameLines[frameLineIndex++]));
        }
      }

      Debug.Assert(frameLines.Length - frameLineIndex == baseLines.Length - baseLineIndex);
      for (int i = 0; i < frameLines.Length - frameLineIndex; ++i) {
        diff.Add((baseLines[baseLineIndex + i], frameLines[frameLineIndex + i]));
      }

      diffs[frameIndex] = diff;
      return lcs;
    }, lcs => { });

    int[] prevIndices = new int[otherFrames.Length];
    int[] paddingCount = new int[otherFrames.Length];

    // reconstruct
    for (int baseIndex = 0; baseIndex < baseLines.Length; ++baseIndex) {
      var baseLine = baseLines[baseIndex];
      for (int diffIndex = 0; diffIndex < diffs.Length; ++diffIndex) {
        var diff = diffs[diffIndex];

        int newLines = 0;
        int prevIndex = prevIndices[diffIndex];

        for (int i = prevIndex; i < diff.Count; ++i, ++newLines) {
          if (diff[i].Item1 == null) {
            // skip
          } else {
            Debug.Assert(object.ReferenceEquals(diff[i].Item1, baseLine));
            break;
          }
        }

        paddingCount[diffIndex] = newLines;
      }

      // this is how many lines need to be insert
      int maxPadding = otherFrames.Length > 0 ? paddingCount.Max() : 0;
      Debug.Assert(maxPadding >= 0);

      for ( int i = 0; i < maxPadding; ++i) {
        baseFrame.Lines.Add(null);
      }
      baseFrame.Lines.Add(baseLine);

      for (int diffIndex = 0; diffIndex < diffs.Length; ++diffIndex) {
        var diff = diffs[diffIndex];
        var padding = paddingCount[diffIndex];

        for (int i = 0; i < padding; ++i) {
          otherFrames[diffIndex].Lines.Add(diff[prevIndices[diffIndex] + i].Item2);
        }

        for (int i = 0; i < maxPadding - padding; ++i) {
          otherFrames[diffIndex].Lines.Add(null);
        }

        otherFrames[diffIndex].Lines.Add(diff[prevIndices[diffIndex] + padding].Item2);

        prevIndices[diffIndex] += padding + 1;
      }
    }

    baseFrame.Initialized = true;
    foreach (var frame in otherFrames) {
      frame.Initialized = true;
    }
  }

  private class LongestCommonSequence {

    public struct DiffChunk {
      public int StartA;
      public int StartB;
      public int AddedA;
      public int AddedB;

      public override string ToString() {
        return $"{StartA}, {StartB}, {AddedA}, {AddedB}";
      }
    }


    private ushort[,] m_c;
    private const int MaxSlice = 5000;

    public LongestCommonSequence() {
    }

    public void Diff<T>(T[] x, T[] y, IEqualityComparer<T> comparer, List<DiffChunk> result) {

      //
      int lowerX = 0;
      int lowerY = 0;
      int upperX = x.Length;
      int upperY = y.Length;

      while (lowerX < upperX && lowerY < upperY && comparer.Equals(x[lowerX], y[lowerY])) {
        ++lowerX;
        ++lowerY;
      }

      while (lowerX < upperX && lowerY < upperY && comparer.Equals(x[upperX - 1], y[upperY - 1])) {
        // pending add
        --upperX;
        --upperY;
      }

      int x1;
      int y1;

      // this is not strictly correct, but LCS is memory hungry; let's just split into slices
      for (int x0 = lowerX, y0 = lowerY; x0 < upperX || y0 < upperY; x0 = x1, y0 = y1) {
        x1 = Mathf.Min(upperX, x0 + MaxSlice);
        y1 = Mathf.Min(upperY, y0 + MaxSlice);

        if (x0 == x1) {
          result.Add(new DiffChunk() { StartA = x0, StartB = y0, AddedB = y1 - y0 });
        } else if (y0 == y1) {
          result.Add(new DiffChunk() { StartA = x0, StartB = y0, AddedA = x1 - x0});
        } else {
          var sx = new ArraySegment<T>(x, x0, x1 - x0);
          var sy = new ArraySegment<T>(y, y0, y1 - y0);

          AllocateMatrix(x1 - x0, y1 - y0);
          FillMatrix(m_c, sx, sy, comparer);
          FillDiff(m_c, sx, sy, comparer, result);
          var chunks = new List<DiffChunk>();
          FillDiff(m_c, sx, sy, comparer, chunks);
        }
      }
    }

    private void AllocateMatrix(int x, int y) {
      if (m_c == null) {
        m_c = new ushort[x + 1, y + 1];
      } else {
        int len0 = Math.Max(m_c.GetLength(0), x + 1);
        int len1 = Math.Max(m_c.GetLength(1), y + 1);
        if (len0 > m_c.GetLength(0) || len1 > m_c.GetLength(1)) {
          m_c = new ushort[len0, len1];
        }
      }
    }

    private static void FillMatrix<T>(ushort[,] c, ArraySegment<T> x, ArraySegment<T> y, IEqualityComparer<T> comparer) {
      int xcount = x.Count;
      int ycount = y.Count;
      int xoffset = x.Offset - 1;
      int yoffset = y.Offset - 1;

      for (int i = 1; i <= xcount; i++) {
        c[i, 0] = 0;
      }
      for (int i = 1; i <= ycount; i++) {
        c[0, i] = 0;
      }

      for (int i = 1; i <= xcount; i++) {
        for (int j = 1; j <= ycount; j++) {
          if (comparer.Equals(x.Array[i + xoffset], y.Array[j + yoffset])) {
            c[i, j] = (ushort)(c[i - 1, j - 1] + 1);
          } else {
            c[i, j] = Math.Max(c[i - 1, j], c[i, j - 1]);
          }
        }
      }
    }

    private static void FillDiff<T>(ushort[,] c, ArraySegment<T> x, ArraySegment<T> y, IEqualityComparer<T> comparer, List<DiffChunk> result) {
      int startIndex = result.Count;
      int i = x.Count - 1;
      int j = y.Count - 1;

      var chunk = new DiffChunk();
      chunk.StartA = x.Offset + x.Count;
      chunk.StartB = y.Offset + y.Count;

      while (i >= 0 || j >= 0) {
        if (i >= 0 && j >= 0 && comparer.Equals(x.Array[x.Offset + i], y.Array[y.Offset + j])) {
          if (chunk.AddedA != 0 || chunk.AddedB != 0) {
            result.Add(chunk);
            chunk = default;
          }
          chunk.StartA = i + x.Offset;
          chunk.StartB = j + y.Offset;
          --i;
          --j;
        } else if (j >= 0 && (i < 0 || c[i + 1, j] >= c[i, j + 1])) {
          Debug.Assert(chunk.AddedA == 0);
          chunk.AddedB++;
          chunk.StartB = j + y.Offset;
          --j;
        } else if (i >= 0 && (j < 0 || c[i + 1, j] < c[i, j + 1])) {
          chunk.AddedA++;
          chunk.StartA = i + x.Offset;
          --i;
        } else {
          throw new NotSupportedException();
        }
      }

      if (chunk.AddedA != 0 || chunk.AddedB != 0) {
        result.Add(chunk);
      }
      result.Reverse(startIndex, result.Count - startIndex);
    }
  }

}
