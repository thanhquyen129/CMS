/** Client-safe severity labels (no server imports). */

export function severityLabel(severity: string): string {
  switch (severity.toLowerCase()) {
    case "critical":
      return "Nghiêm trọng";
    case "high":
      return "Cao";
    case "medium":
      return "Trung bình";
    case "low":
      return "Thấp";
    default:
      return severity;
  }
}
