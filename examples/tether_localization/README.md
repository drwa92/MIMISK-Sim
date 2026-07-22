# Tether Localization Examples

Tether localization examples can use:

```text
/mimisk/tether/state
/mimisk/minirov/depth
/mimisk/drone/pose
/mimisk/minirov/pose     # ground truth for evaluation
```

Suggested baseline: estimate MiniROV horizontal range from deployed tether length and measured depth, then compare with ground truth.
