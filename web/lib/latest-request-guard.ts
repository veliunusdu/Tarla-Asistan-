export class LatestRequestGuard {
  private sequence = 0;

  begin(): number {
    this.sequence += 1;
    return this.sequence;
  }

  isCurrent(requestId: number): boolean {
    return requestId === this.sequence;
  }

  invalidate(requestId: number): void {
    if (this.isCurrent(requestId)) {
      this.sequence += 1;
    }
  }
}
