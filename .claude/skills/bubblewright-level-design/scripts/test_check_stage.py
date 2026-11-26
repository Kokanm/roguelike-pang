#!/usr/bin/env python3
"""Tests for check_stage.py. Run: python3 .claude/skills/bubblewright-level-design/scripts/test_check_stage.py"""
import io
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import check_stage  # noqa: E402

REPO = Path(__file__).resolve().parents[4]
STAGES = REPO / "Assets" / "Bubblewright" / "Scripts" / "Stage" / "Stages"

GOOD = '''using UnityEngine.Scripting;
namespace Bubblewright
{
    [Preserve]
    public sealed class W9_Probe : StageSpec
    {
        public override string Id => "w9_probe";
        public override string Name => "Probe";
        public override int World => 2;
        public override int Difficulty => 2;
        public override string Concept => "A probe.";
        ROWS
        public override string Ground => GROUND;
        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(3);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 20, 8f, -1);
        }
    }
}
'''

SKETCH = '''public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                BODY
                return StageSketch.Lines(grid);
            }
        }'''


def literal_rows(rows):
    return "public override string[] Rows => new[]\n        {\n" + "".join(f'            "{r}",\n' for r in rows) + "        };"


def stage(rows=None, ground="SolidGround", body=None, edit=None):
    if body is not None:
        rows_src = SKETCH.replace("BODY", body)
    else:
        rows_src = literal_rows(rows if rows is not None else ["." * 32] * 14)
    src = GOOD.replace("ROWS", rows_src).replace("GROUND", ground)
    return edit(src) if edit else src


class CheckStageTests(unittest.TestCase):
    def run_on(self, src):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / "W9_Probe.cs"
            path.write_text(src, encoding="utf-8")
            out = io.StringIO()
            with redirect_stdout(out):
                code = check_stage.main([str(path)])
            return code, out.getvalue()

    def assert_fails(self, src, *needles):
        code, out = self.run_on(src)
        self.assertEqual(1, code, out)
        for needle in needles:
            self.assertIn(needle, out)
        return out

    def test_shipped_stages_pass(self):
        out = io.StringIO()
        with redirect_stdout(out):
            code = check_stage.main([str(STAGES)])
        self.assertEqual(0, code, out.getvalue())

    def test_good_probe_passes(self):
        code, out = self.run_on(stage())
        self.assertEqual(0, code, out)
        code, out = self.run_on(stage(body="StageSketch.Box(grid, 9, 22, 4, 4, '#'); grid[4][15] = '.';"))
        self.assertEqual(0, code, out)

    def test_short_and_long_rows_name_the_row_and_draw_the_ruler(self):
        rows = ["." * 32] * 14
        rows[3] = "." * 31
        rows[9] = "." * 33
        out = self.assert_fails(stage(rows), "Row 10 has 31 characters", "Row 4 has 33 characters")
        self.assertIn("01234567890123456789012345678901", out)
        self.assertIn("<-- 31 chars", out)

    def test_row_count_must_be_14_28_or_42_and_match_storeys(self):
        self.assert_fails(stage(["." * 32] * 13), "Rows has 13 lines")
        self.assert_fails(stage(["." * 32] * 28), "Storeys is 1")

    def test_unknown_tile_and_ground_characters(self):
        rows = ["." * 32] * 14
        rows[13] = "....=..........................."
        self.assert_fails(stage(rows), "'='", "column 4")
        self.assert_fails(stage(ground='"' + "=" * 15 + "#" + "=" * 16 + '"'), "Ground has '#' at column 15")
        self.assert_fails(stage(ground='"' + "=" * 31 + '"'), "Ground has 31 characters")

    def test_sketch_writes_must_stay_inside_the_grid(self):
        self.assert_fails(stage(body="StageSketch.Box(grid, 30, 32, 0, 0, '#');"), "c1 = 32")
        self.assert_fails(stage(body="StageSketch.Box(grid, 3, 5, 0, 14, '#');"), "y1 = 14")
        self.assert_fails(stage(body="grid[2][32] = '#';"), "column 32")
        self.assert_fails(stage(body="StageSketch.Box(grid, 3, 5, 0, 1, 'X');"), "'X'")
        self.assert_fails(stage(body="StageSketch.Box(grid, 5, 3, 0, 1, '#');"), "low end first")

    def test_loops_and_ternaries_are_followed(self):
        body = "for (int y = 0; y <= 13; y++) grid[y][4] = y == 13 ? 'T' : 'H';"
        code, out = self.run_on(stage(body=body))
        self.assertEqual(0, code, out)
        body = "for (int y = 0; y <= 14; y++) { grid[y][4] = 'H'; }"
        self.assert_fails(stage(body=body), "index 14")

    def test_metadata_preserve_sealed_and_concept(self):
        self.assert_fails(stage(edit=lambda s: s.replace("[Preserve]", "")), "[Preserve]")
        self.assert_fails(stage(edit=lambda s: s.replace("public sealed class", "public class")), "sealed")
        self.assert_fails(stage(edit=lambda s: s.replace('"A probe."', '""')), "Concept is empty")
        self.assert_fails(stage(edit=lambda s: s.replace('public override string Concept => "A probe.";', "")), "Concept is missing")

    def test_placements_must_be_inside_the_arena(self):
        self.assert_fails(stage(edit=lambda s: s.replace("b.WickAtCol(3);", "b.WickAtCol(32);")), "column 32")
        self.assert_fails(stage(edit=lambda s: s.replace("20, 8f", "20, 15f")), "y = 15.0 is outside 0..14")
        self.assert_fails(stage(edit=lambda s: s.replace("b.WickAtCol(3);", "")), "never places Wick")

    def test_two_storey_sketch(self):
        src = stage(body="StageSketch.Box(grid, 0, 31, 14, 14, '#'); grid[14][4] = 'T';",
                    edit=lambda s: s.replace("StageSketch.Open()", "StageSketch.Open(2)").replace(
                        "public override string Concept", "public override int Storeys => 2;\n        public override string Concept"))
        code, out = self.run_on(src.replace("b.WickAtCol(3);", "b.WickAtCol(3);\n            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 20, 22f, -1);"))
        self.assertEqual(0, code, out)


if __name__ == "__main__":
    unittest.main(verbosity=1)
