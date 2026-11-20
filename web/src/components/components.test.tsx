import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { Button } from './Button'
import { Input } from './Input'
import { Modal } from './Modal'
import { StatusBadge } from './StatusBadge'
import { Table } from './Table'
import { ToastProvider } from './ToastProvider'
import { useToast } from './useToast'

describe('ortak bileşenler', () => {
  it('Button tıklamayı iletir ve varyant sınıfını taşır', async () => {
    const user = userEvent.setup()
    let clicked = false
    render(
      <Button variant="danger" onClick={() => (clicked = true)}>
        Sil
      </Button>,
    )

    await user.click(screen.getByRole('button', { name: 'Sil' }))

    expect(clicked).toBe(true)
    expect(screen.getByRole('button')).toHaveClass('button-danger')
  })

  it('Input etiketi alana bağlar ve hatayı duyurur', () => {
    render(<Input label="E-posta" error="E-posta zorunludur." />)

    const field = screen.getByLabelText('E-posta')
    expect(field).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('alert')).toHaveTextContent('E-posta zorunludur.')
  })

  it('Table satırları ve boş durumu gösterir', () => {
    const columns = [{ key: 'name', header: 'Ad', render: (row: { name: string }) => row.name }]
    const { rerender } = render(<Table columns={columns} rows={[{ name: 'Ayşe' }]} rowKey={(row) => row.name} />)

    expect(screen.getByRole('columnheader', { name: 'Ad' })).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: 'Ayşe' })).toBeInTheDocument()

    rerender(<Table columns={columns} rows={[]} rowKey={(row) => row.name} />)
    expect(screen.getByText('Gösterilecek kayıt yok.')).toBeInTheDocument()
  })

  it('Modal Escape ile kapanır', async () => {
    const user = userEvent.setup()
    function Harness() {
      const [open, setOpen] = useState(true)
      return (
        <Modal open={open} title="Onay" onClose={() => setOpen(false)}>
          <p>Emin misiniz?</p>
        </Modal>
      )
    }
    render(<Harness />)

    expect(screen.getByRole('dialog', { name: 'Onay' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('Toast mesajı ekrana çıkar', async () => {
    const user = userEvent.setup()
    function Harness() {
      const show = useToast()
      return <Button onClick={() => show('Kaydedildi.', 'success')}>Kaydet</Button>
    }
    render(
      <ToastProvider>
        <Harness />
      </ToastProvider>,
    )

    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Kaydedildi.')
  })

  it('StatusBadge anlamı metinle birlikte taşır', () => {
    render(<StatusBadge tone="warning">Yüksek risk</StatusBadge>)

    expect(screen.getByText('Yüksek risk')).toHaveClass('badge-warning')
  })
})