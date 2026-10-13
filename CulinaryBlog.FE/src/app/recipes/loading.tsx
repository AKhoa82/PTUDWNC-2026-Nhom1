export default function Loading() {
  return <main className="mx-auto w-full max-w-7xl space-y-8 px-4 py-10 sm:px-6 lg:px-8" aria-busy="true">
    <div className="space-y-3 border-b border-[#E5DECE] pb-6"><div className="h-3 w-36 animate-pulse rounded bg-[#E5DECE]" /><div className="h-10 w-80 max-w-full animate-pulse rounded bg-[#E5DECE]" /></div>
    <div className="h-40 animate-pulse rounded-2xl border border-[#E5DECE] bg-[#FBF8F1]" />
    <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">{Array.from({ length: 6 }, (_, index) => <div key={index} className="overflow-hidden rounded-xl border border-[#E5DECE] bg-[#FBF8F1]"><div className="aspect-[8/5] animate-pulse bg-[#E5DECE]" /><div className="space-y-3 p-5"><div className="h-5 w-3/4 animate-pulse rounded bg-[#E5DECE]" /><div className="h-3 w-full animate-pulse rounded bg-[#E5DECE]" /><div className="h-3 w-1/2 animate-pulse rounded bg-[#E5DECE]" /></div></div>)}</div>
  </main>;
}
