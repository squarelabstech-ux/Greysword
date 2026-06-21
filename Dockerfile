FROM python:3.9-slim

WORKDIR /app

# Copy project files
COPY antigravity_brain_server.py .
COPY antigravity_master_history.log .

# Expose port 7860 (required by Hugging Face Spaces)
EXPOSE 7860
ENV PORT=7860

# Run the server
CMD ["python", "antigravity_brain_server.py"]
