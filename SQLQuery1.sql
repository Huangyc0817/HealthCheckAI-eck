UPDATE PatientFiles
SET AiSummary = NULL
WHERE AiSummary LIKE '%?%';
