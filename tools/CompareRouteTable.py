#!/usr/bin/env python3
"""Compare the design table with the route table the game loads.

The design document and config/western_mainlines.json have to name the same
figure for every act of every line. When they drift, the code looks correct
against one of them and wrong against the other, which is how a change gets
accepted on one side and rejected on the other.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOC = ROOT / "docs/design/current/west/00西哲当前路线.md"
TABLE = ROOT / "config/western_mainlines.json"

NAMES = {
    "笛卡尔": "DESCARTES", "洛克": "JOHN_LOCKE", "休谟": "DAVID_HUME",
    "康德": "IMMANUEL_KANT", "黑格尔": "GEORG_WILHELM_FRIEDRICH_HEGEL",
    "马克思": "KARL_MARX", "赫拉克利特": "HERACLITUS", "斯宾诺莎": "BARUCH_SPINOZA",
    "谢林": "FRIEDRICH_WILHELM_JOSEPH_SCHELLING", "德勒兹": "GILLES_DELEUZE",
    "苏格拉底": "SOCRATES", "叔本华": "ARTHUR_SCHOPENHAUER", "尼采": "FRIEDRICH_NIETZSCHE",
    "克尔凯郭尔": "SOREN_KIERKEGAARD", "萨特": "JEAN_PAUL_SARTRE",
    "胡塞尔": "EDMUND_HUSSERL", "海德格尔": "MARTIN_HEIDEGGER",
    "梅洛庞蒂": "MAURICE_MERLEAU_PONTY", "亚里士多德": "ARISTOTLE",
    "弗雷格": "GOTTLOB_FREGE", "罗素": "BERTRAND_RUSSELL",
    "维特根斯坦": "LUDWIG_WITTGENSTEIN", "索绪尔": "FERDINAND_DE_SAUSSURE",
    "列维斯特劳斯": "CLAUDE_LEVI_STRAUSS", "弗洛伊德": "SIGMUND_FREUD",
    "拉康": "JACQUES_LACAN", "卢梭": "ROUSSEAU",
    "沃斯通克拉夫特": "MARY_WOLLSTONECRAFT", "波伏瓦": "SIMONE_DE_BEAUVOIR",
    "柏拉图": "PLATO", "孟德斯鸠": "MONTESQUIEU", "边沁": "JEREMY_BENTHAM",
    "密尔": "JOHN_STUART_MILL", "亚当斯密": "ADAM_SMITH",
    "霍克海默": "MAX_HORKHEIMER", "福柯": "MICHEL_FOUCAULT",
    "培根": "FRANCIS_BACON", "牛顿": "ISAAC_NEWTON", "孔德": "AUGUSTE_COMTE",
    "皮尔士": "CHARLES_SANDERS_PEIRCE", "杜威": "JOHN_DEWEY",
}


def thinker(cell):
    # A cell reads like "苏格拉底·德性" or "康德〔制度〕"; the figure is the
    # part before the qualifier.
    text = cell.strip()
    for stage in ("早期", "后期"):
        if text.startswith(stage):
            text = text[len(stage):]
    name = re.split(r"[·〔（(]", text, maxsplit=1)[0].strip()
    return NAMES.get(name)


def main() -> int:
    routes = {route["display_name"]: route for route in json.loads(TABLE.read_text())["routes"]}
    rows = []
    for line in DOC.read_text().splitlines():
        if not line.startswith("| ") or line.startswith("| ---"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) < 4 or cells[0] in ("路线",):
            continue
        rows.append(cells)
    problems = []
    seen = set()
    for cells in rows:
        name, first, second, third = cells[0], cells[1], cells[2], cells[3]
        route = routes.get(name)
        if route is None:
            problems.append(f"{name}: the design table names a line the route table does not have")
            continue
        seen.add(name)
        for act, cell, key in ((1, first, "act_one_figure"), (2, second, "act_two_figure"),
                               (3, third, "act_three_figure")):
            expected = thinker(cell)
            if expected is None:
                problems.append(f"{name} act {act}: cannot read a figure from '{cell}'")
            elif route[key] != expected:
                problems.append(f"{name} act {act}: the design says {expected}, the route table says {route[key]}")
    for name in sorted(set(routes) - seen):
        problems.append(f"{name}: the route table names a line the design table does not have")
    if problems:
        print("\n".join(problems))
        print(f"{len(problems)} disagreement(s) between the design table and the route table.")
        return 1
    print(f"{len(rows)} lines agree across all three acts.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
