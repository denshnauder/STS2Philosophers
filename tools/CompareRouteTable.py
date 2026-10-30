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
    "罗尔斯": "RAWLS", "阿伦特": "HANNAH_ARENDT", "伽达默尔": "HANS_GEORG_GADAMER",
    "德里达": "JACQUES_DERRIDA", "哈贝马斯": "JURGEN_HABERMAS", "阿多诺": "THEODOR_ADORNO",
    "库恩": "THOMAS_KUHN", "巴特勒": "JUDITH_BUTLER", "本雅明": "WALTER_BENJAMIN",
    "利科": "PAUL_RICOEUR", "詹姆斯": "WILLIAM_JAMES",
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


PROBLEM_WORDS = {
    "语言": "LANGUAGE_AND_MEANING",
    "德性": "VIRTUE_AND_HAPPINESS",
    "知识": "KNOWLEDGE_AND_DOUBT",
    "制度": "FREEDOM_AND_INSTITUTIONS",
    "历史": "HISTORY_AND_POWER",
    "存在": "BEING_AND_CHANGE",
    "自我": "SELF_AND_OTHER",
}

QUALIFIERS = {
    "历史": "HISTORY_AND_POWER",
    "历史与制度": "HISTORY_AND_POWER",
    "存在与变化": "BEING_AND_CHANGE",
    "制度": "FREEDOM_AND_INSTITUTIONS",
    "主体／自律": "SELF_AND_OTHER",
    "主体/自律": "SELF_AND_OTHER",
}

# The row may qualify the question ("问号胡塞尔且选择具身方向后"): the extra
# clause says which direction the turn takes, not that it is not a turn.
BRANCH = re.compile(
    r"第一幕问号([^，且]+?)(?:且[^，]*?)?(?:转线后)?，第二幕固定([^、]+?)、第三幕固定([^。；]+)")


def node_for(graph, figure_cell, fallback_problem):
    """Find the node the document means by a figure cell."""
    text = figure_cell.strip()
    for stage in ("早期", "后期"):
        if text.startswith(stage):
            text = text[len(stage):]
    text = text.split("或")[0].strip()
    for suffix in ("二选一", "之一"):
        if text.endswith(suffix):
            text = text[:-len(suffix)]
    parts = re.split(r"[〔（(]", text, maxsplit=1)
    head = parts[0].strip()
    qualifier = re.sub(r"[〕）)]", "", parts[1]) if len(parts) > 1 else ""
    name = re.split(r"[·]", head, maxsplit=1)[0].strip()
    if name not in NAMES:
        # A cell can run the figure and the problem together: 康德制度.
        for known in sorted(NAMES, key=len, reverse=True):
            if head.startswith(known):
                name, qualifier = known, head[len(known):] or qualifier
                break
    thinker_id = NAMES.get(name)
    if thinker_id is None:
        return None
    candidates = [node for node in graph["nodes"] if node["thinker_id"] == thinker_id]
    if not candidates:
        return None
    problem = QUALIFIERS.get(qualifier) or PROBLEM_WORDS.get(qualifier)
    if problem:
        match = [node for node in candidates if node["problem_id"] == problem]
        if match:
            return match[0]
    for preferred in (fallback_problem, "SELF_AND_OTHER", "HISTORY_AND_POWER"):
        match = [node for node in candidates if node["problem_id"] == preferred]
        if match:
            return match[0]
    return candidates[0]


# The document describes each line's first-act question as shifting the line by
# one act: the question puts that figure in act one, and the two fixed figures
# move down. The graph instead stores the default order (the table's columns),
# so these branches cannot be walked. This is a known design gap, not drift;
# any other line that grows a branch problem still fails the check.
KNOWN_BRANCH_GAPS = set()


# One line cannot be chained from the graph: the document gives it an early and
# a late Wittgenstein, and the graph has a single node for him. Splitting the
# node is a content decision, so this stays a known gap.
KNOWN_CHAIN_GAPS = {
    "日常言说": "the graph has one Wittgenstein node, so his early and late stages cannot be two acts",
}


def default_chain_problems(routes, graph):
    """The table's three columns are the default order, so the graph needs a
    fixed edge for act two and act three of every line. Without them the line
    exists in the table and nowhere the player can walk."""
    by_thinker = {}
    for node in graph["nodes"]:
        by_thinker.setdefault(node["thinker_id"], []).append(node)
    problems = []
    for route in routes.values():
        figures = [route["act_one_figure"], route["act_two_figure"], route["act_three_figure"]]
        starts, middles, ends = (by_thinker.get(figure, []) for figure in figures)
        walkable = any(
            any(edge["act"] == 2 and edge["slot"] == "Fixed" and edge["from_node_id"] == start["node_id"]
                and edge["to_node_id"] == middle["node_id"] for edge in graph["edges"])
            and any(edge["act"] == 3 and edge["slot"] == "Fixed" and edge["from_node_id"] == middle["node_id"]
                and edge["to_node_id"] == end["node_id"] for edge in graph["edges"])
            for start in starts for middle in middles for end in ends)
        if not walkable:
            problems.append((route["display_name"], " -> ".join(figures)))
    return problems


def branch_problems(rows, routes, graph):
    """The document also says where each line goes when its first-act question
    turns it. Nothing else checks those branches, and a branch the graph cannot
    walk is a line the player can enter and never finish."""
    problems = []
    notes = []
    for cells in rows:
        name, first = cells[0], cells[1]
        route = routes.get(name)
        if route is None or len(cells) < 5:
            continue
        text = cells[4]
        match = BRANCH.search(text)
        if not match:
            continue
        # The route table names the line's entry problem; the table cell's
        # qualifier is a display hint, not the authority. Using the cell alone
        # picked the thinker's first node and reported edges from the wrong act.
        entry_node = f"{route['entry_thinker']}__{route['entry_problem']}__CORE"
        entry_problem = next((node for node in graph["nodes"] if node["node_id"] == entry_node), None)
        if entry_problem is None:
            problems.append(f"{name}: cannot place the entry figure '{first}'")
            continue
        problem_id = entry_problem["problem_id"]
        turn = node_for(graph, match.group(1), problem_id)
        second = node_for(graph, match.group(2), problem_id)
        third = node_for(graph, match.group(3), problem_id)
        if turn is None or second is None or third is None:
            problems.append(f"{name}: cannot place the branch figures '{match.group(1)}' / '{match.group(2)}' / '{match.group(3)}'")
            continue
        route_id = route["route_id"]
        wanted = [
            (1, "Question", entry_problem["node_id"], turn["node_id"]),
            (2, "Fixed", turn["node_id"], second["node_id"]),
            (3, "Fixed", second["node_id"], third["node_id"]),
        ]
        for act, slot, source, target in wanted:
            match_edges = [edge for edge in graph["edges"]
                           if edge["act"] == act and edge["slot"] == slot
                           and edge["from_node_id"] == source and edge["to_node_id"] == target]
            if not match_edges:
                problems.append(f"{name} act {act}: the document's branch needs {source} -> {target} ({slot}), the graph has no such edge")
                continue
            scoped = [edge for edge in match_edges if route_id in (edge.get("route_ids") or [])]
            if not scoped:
                notes.append(f"{name} act {act}: the branch edge {match_edges[0]['edge_id']} is open to every line, not scoped to {route_id}")
    return problems, notes


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
    graph = json.loads((ROOT / "config/western_graph.json").read_text())
    for name, chain in default_chain_problems(routes, graph):
        if name in KNOWN_CHAIN_GAPS:
            print(f"known gap: {name} has no walkable default chain ({chain}): {KNOWN_CHAIN_GAPS[name]}")
        else:
            problems.append(f"{name}: the route table's own order {chain} cannot be walked in the graph")
    branch, notes = branch_problems(rows, routes, graph)
    known = [issue for issue in branch if issue.split(":")[0].split(" act ")[0] in KNOWN_BRANCH_GAPS]
    problems.extend(issue for issue in branch if issue not in known)
    if known:
        print(f"known gap: {len(known)} documented branch step(s) across "
            + f"{len({issue.split(':')[0].split(' act ')[0] for issue in known})} lines describe the act-shift the graph does not store")
    for note in notes:
        print("note: " + note)
    if problems:
        print("\n".join(problems))
        print(f"{len(problems)} disagreement(s) between the design table and the route table.")
        return 1
    print(f"{len(rows)} lines agree across all three acts; {len(known)} branch steps and "
        + f"{len(KNOWN_CHAIN_GAPS)} default chain are known gaps; everything else is walkable.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
