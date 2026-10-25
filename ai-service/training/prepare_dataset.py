"""Prepare expert-labelled text data. No model, pretrained weights or AI provider."""
import argparse
import json
import math
import random
from pathlib import Path


def prepare(paths, output, seed=42):
    records = {}
    parents = {}

    def find(key):
        parents.setdefault(key, key)
        if parents[key] != key:
            parents[key] = find(parents[key])
        return parents[key]

    for path in paths:
        for line in Path(path).read_text(encoding='utf-8-sig').splitlines():
            if not line.strip():
                continue
            row = json.loads(line)
            for name in ('assessmentGroup', 'scriptGroup', 'questionNumber', 'question', 'answer', 'rubric'):
                if not isinstance(row.get(name), str) or not row[name].strip():
                    raise ValueError(f'Missing {name}')
            maximum, marks = row.get('maxMarks'), row.get('marks')
            if not all(isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v) for v in (maximum, marks)) or not 0 <= marks <= maximum or maximum <= 0:
                raise ValueError('Invalid expert mark')
            key = (row['scriptGroup'], row['questionNumber'])
            if key in records:
                if any(records[key][field] != row[field] for field in ('question', 'answer', 'rubric', 'maxMarks', 'marks')):
                    raise ValueError('Conflicting labels: reconcile expert marks before training')
            records[key] = row
            a, s = find('a:' + row['assessmentGroup']), find('s:' + row['scriptGroup'])
            parents[a] = s
    groups = {}
    for row in records.values():
        groups.setdefault(find('a:' + row['assessmentGroup']), []).append(row)
    if len(groups) < 10:
        raise ValueError('Collect at least 10 independent assessment/script groups before preparing three splits; this is a tooling minimum, not a model-readiness threshold')
    keys = sorted(groups)
    random.Random(seed).shuffle(keys)
    train_end, validation_end = int(len(keys) * .7), int(len(keys) * .85)
    splits = {'train': keys[:train_end], 'validation': keys[train_end:validation_end], 'test': keys[validation_end:]}
    output = Path(output)
    if any((output / f'{name}.jsonl').exists() for name in splits):
        raise ValueError('Output split files already exist; choose a new output directory')
    output.mkdir(parents=True, exist_ok=True)
    counts = {}
    for name, selected in splits.items():
        rows = [row for key in selected for row in groups[key]]
        (output / f'{name}.jsonl').write_text('\n'.join(json.dumps(row, ensure_ascii=False) for row in rows) + '\n', encoding='utf-8')
        counts[name] = len(rows)
    (output / 'split-manifest.json').write_text(json.dumps({'seed': seed, 'groups': len(groups), 'examples': counts}, indent=2), encoding='utf-8')
    return counts


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', nargs='+', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--seed', type=int, default=42)
    args = parser.parse_args()
    print(json.dumps(prepare(args.input, args.output, args.seed)))
