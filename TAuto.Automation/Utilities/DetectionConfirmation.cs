using System;

namespace TAuto.Automation.Utilities;

public class DetectionConfirmation
{
    private int _consecutiveHits;
    private readonly int _requiredHits;

    public DetectionConfirmation(int requiredHits)
    {
        _requiredHits = Math.Max(1, requiredHits);
        _consecutiveHits = 0;
    }

    public bool RecordResult(bool isDetected)
    {
        if (isDetected)
        {
            _consecutiveHits++;
        }
        else
        {
            _consecutiveHits = 0;
        }

        return _consecutiveHits >= _requiredHits;
    }

    public void Reset()
    {
        _consecutiveHits = 0;
    }
}
