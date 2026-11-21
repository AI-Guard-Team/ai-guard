import { describe, expect, it, vi } from 'vitest'
import { chosenOptions, flushAnswers, nextSeq, type PendingAnswer, type SendResult } from './answerQueue'

function answer(seq: number, questionId = 'soru-1', selectedOption: number | null = 0): PendingAnswer {
  return { sessionId: 'oturum', questionId, selectedOption, seq, clientTime: '2026-11-21T10:00:00.000Z' }
}

describe('cevap kuyruğu', () => {
  it('yeni seq sunucudaki son numaranın ve kuyruğun ilerisindedir', () => {
    expect(nextSeq(4, [])).toBe(5)
    expect(nextSeq(4, [answer(7), answer(6)])).toBe(8)
    expect(nextSeq(9, [answer(3)])).toBe(10)
  })

  it('aynı soruda yüksek seq geçerlidir', () => {
    const chosen = chosenOptions(
      [{ questionId: 'soru-1', selectedOption: 1, seq: 2 }],
      [answer(5, 'soru-1', 0), answer(3, 'soru-2', 1)],
    )

    expect(chosen.get('soru-1')).toBe(0)
    expect(chosen.get('soru-2')).toBe(1)
  })

  it('kuyruğu seq sırasında gönderir ve ağ kesilince durur', async () => {
    const sent: number[] = []
    const send = vi.fn(async (item: PendingAnswer): Promise<SendResult> => {
      if (item.seq === 3) return 'stop'
      sent.push(item.seq)
      return 'sent'
    })

    const result = await flushAnswers([answer(3), answer(1), answer(2)], send)

    expect(sent).toEqual([1, 2])
    expect(result.stopped).toBe(true)
    expect(result.sent.map((item) => item.seq)).toEqual([1, 2])
  })

  it('reddedilen cevabı atlayıp sıradakini gönderir', async () => {
    const send = vi.fn(async (item: PendingAnswer): Promise<SendResult> => (item.seq === 1 ? 'drop' : 'sent'))

    const result = await flushAnswers([answer(1), answer(2)], send)

    expect(result.stopped).toBe(false)
    expect(result.sent.map((item) => item.seq)).toEqual([2])
  })
})
