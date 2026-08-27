document.addEventListener('DOMContentLoaded', () => {
  requireAuth();
  document.getElementById('chatForm')?.addEventListener('submit', handleSendMessage);
});

async function handleSendMessage(e) {
  if (e) e.preventDefault();
  const input = document.getElementById('chatInput');
  const message = input?.value.trim();
  if (!message) return;

  appendMessage(message, 'user');
  if (input) input.value = '';

  const typingId = appendTypingIndicator();

  try {
    const res = await api.post('/chat', { message });
    removeTypingIndicator(typingId);
    appendMessage(res.answer, 'ai');
  } catch (err) {
    removeTypingIndicator(typingId);
    appendMessage(err.message || 'Sorry, I encountered an error processing your query.', 'ai');
  }
}

function sendQuickPrompt(promptText) {
  const input = document.getElementById('chatInput');
  if (input) {
    input.value = promptText;
    handleSendMessage();
  }
}

function appendMessage(text, sender) {
  const container = document.getElementById('chatMessagesBox');
  if (!container) return;

  const msgDiv = document.createElement('div');
  msgDiv.className = `chat-message ${sender}-message d-flex gap-3 mb-3`;

  const formattedText = text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');

  if (sender === 'user') {
    msgDiv.innerHTML = `
      <div class="chat-bubble user-bubble ms-auto bg-primary text-white p-3 rounded-lg">
        ${formattedText}
      </div>
      <div class="chat-avatar user-avatar"><i class="fa-solid fa-user"></i></div>
    `;
  } else {
    msgDiv.innerHTML = `
      <div class="chat-avatar ai-avatar"><i class="fa-solid fa-robot"></i></div>
      <div class="chat-bubble ai-bubble bg-light p-3 rounded-lg border">
        ${formattedText}
      </div>
    `;
  }

  container.appendChild(msgDiv);
  container.scrollTop = container.scrollHeight;
}

function appendTypingIndicator() {
  const container = document.getElementById('chatMessagesBox');
  if (!container) return null;

  const id = `typing-${Date.now()}`;
  const div = document.createElement('div');
  div.id = id;
  div.className = 'chat-message ai-message d-flex gap-3 mb-3';
  div.innerHTML = `
    <div class="chat-avatar ai-avatar"><i class="fa-solid fa-robot"></i></div>
    <div class="chat-bubble ai-bubble bg-light p-3 rounded-lg border text-muted">
      <i class="fa-solid fa-ellipsis fa-bounce me-2"></i> Analyzing SQL financial data...
    </div>
  `;

  container.appendChild(div);
  container.scrollTop = container.scrollHeight;
  return id;
}

function removeTypingIndicator(id) {
  if (!id) return;
  const el = document.getElementById(id);
  if (el) el.remove();
}
