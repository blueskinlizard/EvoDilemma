from flask import Flask, request, jsonify
from model_def import AgentModel
import torch
import torch.nn as nn
import numpy as np

app = Flask(__name__)
model = AgentModel()
model.eval()    

@app.route('/forward', methods=['POST'])
def forward_pass():
    data = request.get_json()
    inputs = data.get('inputs')  # Our list of 6 floats provided by the agent (3 for what moves agent played against opponent, 3 vice versa)
    if not inputs or len(inputs) != 6: # This shouldn't really happen, but just in case yk
        return jsonify({'error': 'Invalid inputs'}), 400
    tensor_input = torch.tensor([inputs], dtype=torch.float32)
    with torch.no_grad():
        output = model(tensor_input)
        probs = torch.softmax(output, dim=1).numpy()[0]
        action = int(np.argmax(probs)) # Binary output (prisoner's dilemma has only two options)
    return jsonify({'action': action, 'probabilities': probs.tolist()})

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=8080)