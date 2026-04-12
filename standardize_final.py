import sys, json
from pathlib import Path
from graphify.build import build_from_json
from graphify.cluster import cluster, score_all
from graphify.analyze import god_nodes, surprising_connections, suggest_questions
from graphify.report import generate
from graphify.export import to_json, to_html

def standardize():
    # 1. Merge Chunks
    print("Merging chunks...")
    ast = json.loads(Path('.graphify_ast.json').read_text())
    c1 = json.loads(Path('.graphify_chunk_1.json').read_text())
    
    seen = {n['id'] for n in ast['nodes']}
    merged_nodes = list(ast['nodes'])
    for sem in [c1]:
        for n in sem['nodes']:
            if n['id'] not in seen:
                merged_nodes.append(n)
                seen.add(n['id'])
    
    merged_edges = ast['edges'] + c1['edges']
    merged_tokens = {
        'input': ast.get('input_tokens',0) + c1.get('input_tokens',0),
        'output': ast.get('output_tokens',0) + c1.get('output_tokens',0)
    }
    
    merged = {
        'nodes': merged_nodes,
        'edges': merged_edges,
        'hyperedges': [],
        'input_tokens': merged_tokens['input'],
        'output_tokens': merged_tokens['output'],
    }
    Path('.graphify_extract.json').write_text(json.dumps(merged, indent=2), encoding='utf-8')

    # 2. Build & Cluster
    print("Building graph & Clustering...")
    G = build_from_json(merged)
    communities = cluster(G)
    cohesion = score_all(G, communities)
    gods = god_nodes(G)
    surprises = surprising_connections(G, communities)
    
    labels = {cid: f"TAuto Core: {cid}" for cid in communities}
    # Specific TAuto labels
    for cid, nodes in communities.items():
        if any('StateMachine' in str(n) for n in nodes): labels[cid] = "State Machine Framework"
        elif any('Action' in str(n) for n in nodes): labels[cid] = "Automation Actions"
        elif any('Context' in str(n) for n in nodes): labels[cid] = "Runtime Context"

    Path('.graphify_labels.json').write_text(json.dumps({str(k): v for k, v in labels.items()}), encoding='utf-8')

    # 3. Analyze & Save
    Path('.graphify_analysis.json').write_text(json.dumps({
        'communities': {str(k): v for k, v in communities.items()},
        'gods': gods,
        'surprises': surprises
    }, indent=2), encoding='utf-8')

    # 4. Export
    print("Exporting finalized files...")
    Path('graphify-out').mkdir(exist_ok=True)
    to_json(G, communities, 'graphify-out/graph.json')
    to_html(G, communities, 'graphify-out/graph.html', community_labels=labels)
    
    detect = json.loads(Path('.graphify_detect.json').read_text(encoding='utf-8'))
    # Mocking metadata required by the standardized report generator
    detect['total_files'] = len(detect['files']['code']) + len(detect['files']['document'])
    detect['total_words'] = 50000 # Estimate
    
    report = generate(G, communities, cohesion, labels, gods, surprises, detect, merged_tokens, '.', suggested_questions=[])
    Path('graphify-out/GRAPH_REPORT.md').write_text(report, encoding='utf-8')
    
    print("TAuto Standardization Complete.")

if __name__ == "__main__":
    standardize()
