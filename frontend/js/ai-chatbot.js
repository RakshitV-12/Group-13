// =========================================================================
// AI Financial Insights Chatbot Widget
// =========================================================================

document.addEventListener('DOMContentLoaded', () => {
  const fab = document.getElementById('aiChatFab');
  const drawer = document.getElementById('aiChatDrawer');
  const closeBtn = document.getElementById('closeChatBtn');
  const chatForm = document.getElementById('chatForm');
  const chatInput = document.getElementById('chatInput');

  if (fab && drawer) {
    fab.addEventListener('click', () => {
      drawer.classList.toggle('open');
      if (drawer.classList.contains('open') && chatInput) {
        chatInput.focus();
      }
    });
  }

  if (closeBtn && drawer) {
    closeBtn.addEventListener('click', () => {
      drawer.classList.remove('open');
    });
  }

  if (chatForm) {
    chatForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const message = chatInput.value.trim();
      if (!message) return;

      appendChatMessage(message, 'user');
      chatInput.value = '';

      // Typing indicator
      const typingId = appendChatMessage('Thinking...', 'bot', true);

      try {
        const response = await api.post('/ai/chat', { message });
        removeChatMessage(typingId);
        appendChatMessage(response.answer, 'bot');
      } catch (err) {
        removeChatMessage(typingId);
        appendChatMessage('Sorry, I encountered an issue retrieving your financial data.', 'bot');
      }
    });
  }
});

function sendQuickPrompt(prompt) {
  const chatInput = document.getElementById('chatInput');
  if (chatInput) {
    chatInput.value = prompt;
    document.getElementById('chatForm').dispatchEvent(new Event('submit'));
  }
}

function appendChatMessage(text, sender, isTyping = false) {
  const body = document.getElementById('chatBody');
  if (!body) return null;

  const id = 'msg_' + Date.now();
  const bubble = document.createElement('div');
  bubble.id = id;
  bubble.className = `chat-bubble ${sender}`;
  bubble.innerHTML = text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
  body.appendChild(bubble);
  body.scrollTop = body.scrollHeight;

  return id;
}

function removeChatMessage(id) {
  if (!id) return;
  const el = document.getElementById(id);
  if (el) el.remove();
}
