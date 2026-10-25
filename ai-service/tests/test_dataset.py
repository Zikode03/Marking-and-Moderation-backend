import json
import pytest
from training.prepare_dataset import prepare


def rows():
    return [dict(assessmentGroup=f'paper-{i}', scriptGroup=f'script-{i}', questionNumber='1', question='Q', answer='A', rubric='Guide', maxMarks=10, marks=7) for i in range(12)]


def test_grouped_splits_are_disjoint_and_deduplicated(tmp_path):
    data=rows()
    data.append(data[0].copy())
    data.append(dict(data[0], questionNumber='2'))
    source=tmp_path/'examples.jsonl'
    source.write_text('\n'.join(json.dumps(row) for row in data))
    result=prepare([source],tmp_path/'splits')
    assert sum(result.values()) == 13
    groups=[]
    for name in ['train','validation','test']:
        loaded=[json.loads(line) for line in (tmp_path/'splits'/f'{name}.jsonl').read_text().splitlines()]
        groups.append({row['assessmentGroup'] for row in loaded})
    assert not groups[0] & groups[1] and not groups[0] & groups[2] and not groups[1] & groups[2]
    with pytest.raises(ValueError): prepare([source],tmp_path/'splits')


def test_conflicting_labels_and_insufficient_groups_fail(tmp_path):
    source=tmp_path/'examples.jsonl'
    source.write_text('\n'.join(json.dumps(row) for row in [rows()[0],dict(rows()[0],marks=2)]))
    with pytest.raises(ValueError,match='Conflicting'): prepare([source],tmp_path/'out')
    source.write_text(json.dumps(rows()[0]))
    with pytest.raises(ValueError,match='10 independent'): prepare([source],tmp_path/'out')
