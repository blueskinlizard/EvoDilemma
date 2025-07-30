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

# While we put our file into the unity project for visualization, we need to run the actual prisoner's dilemma logic on our server(which we need the connection list for)
with open('../python_server/ws_graph_edges.json', 'w') as f:
    json.dump(edges, f)

print(f"Graph generated with {num_agents} nodes and saved edges to ws_graph_edges.json")