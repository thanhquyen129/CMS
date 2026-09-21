type Props = {
  label: string;
};

/** Page-slot skeleton only — never redraw the sidebar chrome. */
export function ContentLoading({ label }: Props) {
  return (
    <section className="panel panel-wide" aria-busy="true">
      <div className="skeleton-line w-40" />
      <div className="skeleton-line w-70" />
      <div className="skeleton-block" />
      <p className="muted">{label}</p>
    </section>
  );
}
