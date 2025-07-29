# RUN THIS SCRIPT TO RANDOMIZE AGENT NETWORK
import networkx as nx
import json

num_agents = 200
knn = 4
rewiring_prob = 0.3

generated_agent_network = nx.watts_strogatz_graph(num_agents, knn, rewiring_prob)
edges = list(generated_agent_network.edges())

with open('../../unity/EvoDilemmaUnity/Assets/Resources/ws_graph_edges.json', 'w') as f:
    # Forgive my file paths reader
    json.dump(edges, f)
# Generates indices in format of [0, 1], [0, 99], [0, 44] to JSON file for Unity proj to read

print(f"Graph generated with {num_agents} nodes and saved edges to ws_graph_edges.json")