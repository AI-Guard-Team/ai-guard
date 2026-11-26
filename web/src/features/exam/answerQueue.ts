export interface PendingAnswer {
  sessionId: string
  questionId: string
  selectedOption: number | null
  seq: number
  clientTime: string
}

interface StoredAnswer extends PendingAnswer {
  key: string
}

const DB_NAME = 'aiguard-exam'
const STORE = 'answers'

/** Yeni seq, sunucunun gördüğü son numaranın ve henüz gitmemiş kuyruğun ilerisindedir (K-24). */
export function nextSeq(lastSeq: number, pending: readonly { seq: number }[]): number {
  return pending.reduce((max, item) => Math.max(max, item.seq), lastSeq) + 1
}

/** Aynı soruda en yüksek seq geçerlidir; gönderilmemiş cevap sunucudakinin önüne geçer. */
export function chosenOptions(
  saved: readonly { questionId: string; selectedOption: number | null; seq: number }[],
  pending: readonly PendingAnswer[],
): Map<string, number | null> {
  const best = new Map<string, { option: number | null; seq: number }>()
  const consider = (questionId: string, option: number | null, seq: number) => {
    const current = best.get(questionId)
    if (!current || seq > current.seq) best.set(questionId, { option, seq })
  }
  for (const answer of saved) consider(answer.questionId, answer.selectedOption, answer.seq)
  for (const answer of pending) consider(answer.questionId, answer.selectedOption, answer.seq)
  return new Map([...best].map(([questionId, value]) => [questionId, value.option]))
}

/** `drop`: sunucu reddetti, kuyruktan silindi, sıradakine geçilir. `stop`: ağ yok, kuyruk olduğu yerde kalır. */
export type SendResult = 'sent' | 'drop' | 'stop'

/** Kuyruğu seq sırasında gönderir. Ağ hatasında durur; gönderilenler listeden düşülebilir. */
export async function flushAnswers(
  pending: readonly PendingAnswer[],
  send: (answer: PendingAnswer) => Promise<SendResult>,
): Promise<{ sent: PendingAnswer[]; stopped: boolean }> {
  const sent: PendingAnswer[] = []
  for (const answer of [...pending].sort((a, b) => a.seq - b.seq)) {
    const result = await send(answer)
    if (result === 'stop') return { sent, stopped: true }
    if (result === 'sent') sent.push(answer)
  }
  return { sent, stopped: false }
}

function answerKey(answer: Pick<PendingAnswer, 'sessionId' | 'seq'>): string {
  return `${answer.sessionId}:${answer.seq}`
}

function requestToPromise<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('answer queue read failed'))
  })
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1)
    request.onupgradeneeded = () => {
      const db = request.result
      if (!db.objectStoreNames.contains(STORE)) {
        const store = db.createObjectStore(STORE, { keyPath: 'key' })
        store.createIndex('sessionId', 'sessionId')
      }
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('answer queue open failed'))
  })
}

export async function listPending(sessionId: string): Promise<PendingAnswer[]> {
  const db = await openDb()
  try {
    const tx = db.transaction(STORE, 'readonly')
    const rows = await requestToPromise(tx.objectStore(STORE).index('sessionId').getAll(sessionId))
    return (rows as StoredAnswer[])
      .map(({ sessionId: id, questionId, selectedOption, seq, clientTime }) => ({
        sessionId: id,
        questionId,
        selectedOption,
        seq,
        clientTime,
      }))
      .sort((a, b) => a.seq - b.seq)
  } finally {
    db.close()
  }
}

export async function enqueue(answer: PendingAnswer): Promise<void> {
  const db = await openDb()
  try {
    const tx = db.transaction(STORE, 'readwrite')
    tx.objectStore(STORE).put({ ...answer, key: answerKey(answer) } satisfies StoredAnswer)
    await requestToPromise(tx.objectStore(STORE).get(answerKey(answer)) as IDBRequest<StoredAnswer>)
  } finally {
    db.close()
  }
}

export async function removePending(answer: Pick<PendingAnswer, 'sessionId' | 'seq'>): Promise<void> {
  const db = await openDb()
  try {
    const tx = db.transaction(STORE, 'readwrite')
    tx.objectStore(STORE).delete(answerKey(answer))
    await requestToPromise(tx.objectStore(STORE).get(answerKey(answer)) as IDBRequest<StoredAnswer | undefined>)
  } finally {
    db.close()
  }
}
