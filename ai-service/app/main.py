from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

app = FastAPI(title="Prospera AI Marking Research Service", version="0.1.0")

class MarkingRequest(BaseModel):
    question: str = Field(min_length=1)
    answer: str = Field(min_length=1)
    rubric: str = Field(min_length=1)

@app.get("/health")
def health():
    return {"status": "ok", "model_status": "not_trained", "production_marking_enabled": False}

@app.post("/mark")
def mark(payload: MarkingRequest):
    raise HTTPException(status_code=503, detail="No trained and validated proprietary marking model is available yet. Do not assign AI marks.")
